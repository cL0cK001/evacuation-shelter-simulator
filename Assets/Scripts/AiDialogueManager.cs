using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace EvacuationShelter.Voice
{
    [Serializable]
    public class AiRoleplayResponse
    {
        public string characterId;
        public string characterName;
        public string dialogue;
        public string situation;
        public string emotion; // Normal, Happy, Sad, Angry, Surprised
    }

    public enum AiProviderType
    {
        GoogleGemini,
        GroqFreeAPI,
        OpenRouterFree
    }

    [Serializable]
    internal class OpenAiChatRequest
    {
        public string model;
        public List<OpenAiMessage> messages;
        public ResponseFormat response_format;
    }

    [Serializable]
    internal class OpenAiMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    internal class ResponseFormat
    {
        public string type = "json_object";
    }

    [Serializable]
    internal class GeminiRequest
    {
        public SystemInstruction systemInstruction;
        public List<Content> contents;
        public GenerationConfig generationConfig;
    }

    [Serializable]
    internal class SystemInstruction
    {
        public List<Part> parts;
    }

    [Serializable]
    internal class Content
    {
        public string role;
        public List<Part> parts;
    }

    [Serializable]
    internal class Part
    {
        public string text;
    }

    [Serializable]
    internal class GenerationConfig
    {
        public string responseMimeType = "application/json";
    }

    /// <summary>
    /// Google Gemini / Groq / OpenRouter 等の無料AI APIと通信し、避難所ロールプレイを行う対話マネージャー
    /// </summary>
    public class AiDialogueManager : MonoBehaviour
    {
        [Header("AI プロバイダー設定")]
        [SerializeField] private AiProviderType provider = AiProviderType.GoogleGemini;

        [Header("API Key 設定")]
        [Tooltip("Google AI Studio / Groq / OpenRouter の無料APIキー")]
        [SerializeField] private string apiKey = "";

        [Header("コンポーネント参照")]
        [SerializeField] private CharacterActingController actingController;

        [Header("音声話者ID (VOICEVOX)")]
        [SerializeField] private int defaultSpeakerId = 3; // ずんだもん

        public string ApiKey
        {
            get => apiKey;
            set
            {
                apiKey = value;
                if (!string.IsNullOrEmpty(apiKey))
                {
                    PlayerPrefs.SetString("SAVED_GEMINI_API_KEY", apiKey);
                    PlayerPrefs.Save();
                }
            }
        }

        public AiProviderType Provider
        {
            get => provider;
            set => provider = value;
        }

        private List<Content> chatHistory = new List<Content>();
        private List<OpenAiMessage> openAiHistory = new List<OpenAiMessage>();
        private string lastErrorMessage = "";

        public event Action<AiRoleplayResponse> OnResponseReceived;
        public event Action<string> OnStatusChanged;

        private const string SYSTEM_PROMPT = @"
本システムは、災害時の避難所における要配慮者の状況を体験・シミュレーションするためのロールプレイシステムです。

【役割と規則】
- あなたは選択されたキャラクターになりきって、福祉の専門家であるユーザーと対話します。
- ユーザーが「体験モード」を選択した場合は、下記の5つのキャラクター【1】〜【5】の中からランダムに1人を選択し、その人物になりきってください。
- 出力は必ず以下のJSON形式のみで返答してください。余計なマークダウンや説明テキストは一切含めないでください。

【出力JSONフォーマット】
{
  ""characterId"": ""01〜05"",
  ""characterName"": ""キャラクター名"",
  ""dialogue"": ""発話するセリフ"",
  ""situation"": ""【状況】現在の仕草、行動、表情、周囲の様子"",
  ""emotion"": ""Normal"" | ""Happy"" | ""Sad"" | ""Angry"" | ""Surprised""
}

【キャラクター設定一覧】
1. characterId: '01', 75歳男性, 足の痛み/疲労/軽度認知症疑い。段ボールベッド未設置で床に直寝。暗闇と転倒を恐れトイレ我慢。「家に帰らなきゃ」と徘徊。
2. characterId: '02', 32歳女性, 乳幼児同伴/重度アレルギー(卵・乳)。授乳室・プライバシー不足。子供のぐずりに謝り身を縮める。車中避難を検討。
3. characterId: '03', 60歳女性, 軽度聴覚障害/ペット(小型犬)同伴。アナウンス聞こえず情報孤立。ペットクレームを懸念。相手の口元をじっと見る。
4. characterId: '04', 45歳男性, 視覚障害(全盲)/白杖。障害物で歩行動線なし。トイレ同行を頼めず水分我慢で脱水症状。壁沿いに立つ。
5. characterId: '05', 22歳女性, 外国人(イスラム教徒)/日本語不自由。ハラール・礼拝スペースなし。弁当表示読めず空腹。片言で必死に尋ねる。
";

        private void Awake()
        {
            if (actingController == null)
            {
                actingController = FindObjectOfType<CharacterActingController>();
            }

            if (string.IsNullOrEmpty(apiKey) && PlayerPrefs.HasKey("SAVED_GEMINI_API_KEY"))
            {
                apiKey = PlayerPrefs.GetString("SAVED_GEMINI_API_KEY");
            }
        }

        /// <summary>
        /// 体験モードセッションを開始します（C#側で真のランダムでキャラクター【1】〜【5】を選択）
        /// </summary>
        public async Task StartSessionAsync(string userInitialPrompt = null)
        {
            chatHistory.Clear();
            openAiHistory.Clear();
            openAiHistory.Add(new OpenAiMessage { role = "system", content = SYSTEM_PROMPT });

            if (string.IsNullOrEmpty(userInitialPrompt))
            {
                int randomCharId = UnityEngine.Random.Range(1, 6); // 1〜5 のランダム選択
                userInitialPrompt = $"体験モードを開始してください。今回はキャラクター【{randomCharId:D2}】になりきり、最初の【状況】とセリフを出力してください。";
            }

            await SendUserMessageAsync(userInitialPrompt);
        }

        public async Task SendUserMessageAsync(string userText)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                NotifyStatus("エラー: APIキーが設定されていません。");
                return;
            }

            NotifyStatus("AIが応答・思考中...");
            lastErrorMessage = "";

            string cleanKey = apiKey.Trim().Trim('"').Trim('\'');

            // 自動プロバイダー判定
            if (cleanKey.StartsWith("gsk_"))
            {
                provider = AiProviderType.GroqFreeAPI;
            }
            else if (cleanKey.StartsWith("sk-or-"))
            {
                provider = AiProviderType.OpenRouterFree;
            }

            string responseJson = null;

            if (provider == AiProviderType.GroqFreeAPI || provider == AiProviderType.OpenRouterFree)
            {
                responseJson = await SendOpenAiCompatibleAsync(cleanKey, userText);
            }
            else
            {
                responseJson = await SendGeminiAsync(cleanKey, userText);
            }

            if (string.IsNullOrEmpty(responseJson))
            {
                NotifyStatus($"エラー: AI応答取得失敗 ({lastErrorMessage})");
                return;
            }

            try
            {
                AiRoleplayResponse parsedResponse = JsonUtility.FromJson<AiRoleplayResponse>(ExtractJsonString(responseJson));
                if (parsedResponse != null && !string.IsNullOrEmpty(parsedResponse.dialogue))
                {
                    NotifyStatus($"AI応答受信 [{parsedResponse.characterName}]");
                    OnResponseReceived?.Invoke(parsedResponse);

                    if (actingController != null)
                    {
                        EmotionType emotion = ParseEmotion(parsedResponse.emotion);
                        int speakerId = GetSpeakerIdForCharacter(parsedResponse.characterId);
                        await actingController.PerformActingAsync(parsedResponse.dialogue, emotion, speakerId);
                    }
                }
            }
            catch (Exception ex)
            {
                NotifyStatus($"パースエラー: {ex.Message}");
                Debug.LogError($"[AiDialogueManager] JSON解析エラー: {ex.Message}\nRaw: {responseJson}");
            }
        }

        private async Task<string> SendGeminiAsync(string key, string userText)
        {
            chatHistory.Add(new Content
            {
                role = "user",
                parts = new List<Part> { new Part { text = userText } }
            });

            string[] candidateUrls = new string[] 
            { 
                $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={key}",
                $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={key}",
                $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-8b:generateContent?key={key}"
            };

            foreach (var url in candidateUrls)
            {
                GeminiRequest reqData = new GeminiRequest
                {
                    systemInstruction = new SystemInstruction
                    {
                        parts = new List<Part> { new Part { text = SYSTEM_PROMPT } }
                    },
                    contents = chatHistory,
                    generationConfig = new GenerationConfig()
                };

                string jsonBody = JsonUtility.ToJson(reqData);
                string rawText = await SendHttpRequestAsync(url, "POST", jsonBody, null);

                if (!string.IsNullOrEmpty(rawText))
                {
                    string extractedText = System.Text.RegularExpressions.Regex.Match(rawText, @"(?<=""text"":\s*"").*?(?=""\s*\}|\""\s*\])", System.Text.RegularExpressions.RegexOptions.Singleline).Value;
                    return System.Text.RegularExpressions.Regex.Unescape(extractedText);
                }
            }

            return null;
        }

        private async Task<string> SendOpenAiCompatibleAsync(string key, string userText)
        {
            openAiHistory.Add(new OpenAiMessage { role = "user", content = userText });

            string endpoint = provider == AiProviderType.GroqFreeAPI 
                ? "https://api.groq.com/openai/v1/chat/completions" 
                : "https://openrouter.ai/api/v1/chat/completions";

            string modelName = provider == AiProviderType.GroqFreeAPI 
                ? "llama-3.3-70b-versatile" 
                : "meta-llama/llama-3.3-70b-instruct:free";

            OpenAiChatRequest req = new OpenAiChatRequest
            {
                model = modelName,
                messages = openAiHistory,
                response_format = new ResponseFormat()
            };

            string jsonBody = JsonUtility.ToJson(req);
            string headerAuth = $"Bearer {key}";

            string rawText = await SendHttpRequestAsync(endpoint, "POST", jsonBody, headerAuth);
            if (!string.IsNullOrEmpty(rawText))
            {
                string extractedText = System.Text.RegularExpressions.Regex.Match(rawText, @"(?<=""content"":\s*"").*?(?=""\s*\}|\""\s*\])", System.Text.RegularExpressions.RegexOptions.Singleline).Value;
                extractedText = System.Text.RegularExpressions.Regex.Unescape(extractedText);
                openAiHistory.Add(new OpenAiMessage { role = "assistant", content = extractedText });
                return extractedText;
            }

            return null;
        }

        private async Task<string> SendHttpRequestAsync(string url, string method, string jsonBody, string authHeader)
        {
            using (UnityWebRequest req = new UnityWebRequest(url, method))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                if (!string.IsNullOrEmpty(authHeader))
                {
                    req.SetRequestHeader("Authorization", authHeader);
                }

                var operation = req.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    string errText = req.downloadHandler != null ? req.downloadHandler.text : "";
                    string errDetail = $"[HTTP {req.responseCode}] {req.error}";
                    if (req.responseCode == 429)
                    {
                        errDetail = "API利用制限（429）。少し待ってから再試行してください。";
                    }
                    Debug.LogWarning($"[AiDialogueManager] {url} -> {errDetail} : {errText}");
                    lastErrorMessage = errDetail;
                    return null;
                }

                return req.downloadHandler.text;
            }
        }

        private string ExtractJsonString(string text)
        {
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                return text.Substring(start, end - start + 1);
            }
            return text;
        }

        private EmotionType ParseEmotion(string emotionStr)
        {
            if (Enum.TryParse<EmotionType>(emotionStr, true, out EmotionType result))
            {
                return result;
            }
            return EmotionType.Normal;
        }

        private int GetSpeakerIdForCharacter(string characterId)
        {
            switch (characterId)
            {
                case "01": return 0;  // 高齢者男性
                case "02": return 2;  // 母親
                case "03": return 8;  // 60歳女性
                case "04": return 1;  // 視覚障害男性
                case "05": return 3;  // 外国人女性
                default: return defaultSpeakerId;
            }
        }

        private void NotifyStatus(string message)
        {
            OnStatusChanged?.Invoke(message);
            Debug.Log($"[AiDialogueManager] {message}");
        }
    }
}
