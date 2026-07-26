using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace EvacuationShelter.Voice
{
    /// <summary>
    /// VOICEVOX のローカルAPI (既定: http://localhost:5021) と通信して音声を生成するクライアント
    /// </summary>
    public class VoicevoxClient : MonoBehaviour
    {
        [Header("VOICEVOX 設定")]
        [Tooltip("VOICEVOXエンジンサーバーのURL")]
        [SerializeField] private string serverUrl = "http://127.0.0.1:5021";

        [Tooltip("デフォルトの話者ID (0: 四国めたん, 2: ずんだもん, 8: 春日部つむぎ 等)")]
        [SerializeField] private int defaultSpeakerId = 3; // ずんだもん（あまあま）/ノーマル等

        public string ServerUrl
        {
            get => serverUrl;
            set => serverUrl = value;
        }

        public int DefaultSpeakerId
        {
            get => defaultSpeakerId;
            set => defaultSpeakerId = value;
        }

        /// <summary>
        /// 指定したテキストと話者IDからAudioClipを非同期生成します。
        /// </summary>
        public async Task<AudioClip> GenerateVoiceAsync(string text, int? speakerId = null, float speedScale = 1.0f, float pitchScale = 0.0f, float intonationScale = 1.0f, float volumeScale = 2.0f)
        {
            int speaker = speakerId ?? defaultSpeakerId;

            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.LogWarning("[VoicevoxClient] テキストが空です。");
                return null;
            }

            // Step 1: /audio_query を呼び出してクエリJSONを取得
            string encodedText = UnityWebRequest.EscapeURL(text);
            string queryUrl = $"{serverUrl.TrimEnd('/')}/audio_query?text={encodedText}&speaker={speaker}";

            string audioQueryJson = await SendPostRequestAsync(queryUrl, string.Empty);
            if (string.IsNullOrEmpty(audioQueryJson))
            {
                Debug.LogWarning("[VoicevoxClient] VOICEVOX サーバーに接続できませんでした。Windows 標準音声合成 (SAPI) をフォールバック利用します。");
                return await GenerateWindowsSpeechFallbackAsync(text, volumeScale);
            }

            // パラメータ調整（パラメータの簡易置換）
            audioQueryJson = AdjustAudioQueryJson(audioQueryJson, speedScale, pitchScale, intonationScale, volumeScale);

            // Step 2: /synthesis を呼び出してWAVバイナリを取得
            string synthesisUrl = $"{serverUrl.TrimEnd('/')}/synthesis?speaker={speaker}";
            byte[] wavBytes = await SendPostBinaryRequestAsync(synthesisUrl, audioQueryJson);

            if (wavBytes == null || wavBytes.Length == 0)
            {
                Debug.LogWarning("[VoicevoxClient] synthesis 取得失敗。Windows 標準音声にフォールバックします。");
                return await GenerateWindowsSpeechFallbackAsync(text, volumeScale);
            }

            // Step 3: WAVバイナリをAudioClipに変換
            AudioClip clip = WavUtility.ToAudioClip(wavBytes, $"VOICEVOX_{speaker}_{text.Substring(0, Mathf.Min(text.Length, 10))}");
            return clip;
        }

        /// <summary>
        /// VOICEVOXが起動していない場合のWindows標準音声合成 (SAPI) フォールバック処理
        /// </summary>
        private async Task<AudioClip> GenerateWindowsSpeechFallbackAsync(string text, float volumeBoost = 2.0f)
        {
            try
            {
                string tempPath = System.IO.Path.Combine(Application.temporaryCachePath, "fallback_voice.wav");
                if (System.IO.File.Exists(tempPath))
                {
                    System.IO.File.Delete(tempPath);
                }

                // PowerShell 経由で Windows の System.Speech 音声合成を呼び出して WAV 出力
                string psScript = $"Add-Type -AssemblyName System.Speech; $synth = New-Object System.Speech.Synthesis.SpeechSynthesizer; $synth.Volume = 100; $synth.SetOutputToWaveFile('{tempPath.Replace("\\", "/")}'); $synth.Speak('{text.Replace("'", "''")}'); $synth.Dispose()";
                
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using (var process = System.Diagnostics.Process.Start(psi))
                {
                    while (!process.HasExited)
                    {
                        await Task.Yield();
                    }
                }

                if (System.IO.File.Exists(tempPath))
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(tempPath);
                    AudioClip clip = WavUtility.ToAudioClip(bytes, "WindowsFallbackVoice");
                    if (clip != null && volumeBoost > 1.0f)
                    {
                        AmplifyAudioClip(clip, volumeBoost);
                    }
                    return clip;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoicevoxClient] フォールバック音声の生成に失敗しました: {ex.Message}");
            }

            return null;
        }

        private void AmplifyAudioClip(AudioClip clip, float multiplier)
        {
            float[] data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = Mathf.Clamp(data[i] * multiplier, -1.0f, 1.0f);
            }
            clip.SetData(data, 0);
        }

        private string AdjustAudioQueryJson(string json, float speed, float pitch, float intonation, float volume)
        {
            // JSON内の "speedScale":1, "pitchScale":0, "intonationScale":1, "volumeScale":1 を更新
            json = System.Text.RegularExpressions.Regex.Replace(json, @"(?<=""speedScale"":\s*)[0-9\.]+", speed.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            json = System.Text.RegularExpressions.Regex.Replace(json, @"(?<=""pitchScale"":\s*)[0-9\.-]+", pitch.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            json = System.Text.RegularExpressions.Regex.Replace(json, @"(?<=""intonationScale"":\s*)[0-9\.]+", intonation.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            json = System.Text.RegularExpressions.Regex.Replace(json, @"(?<=""volumeScale"":\s*)[0-9\.]+", volume.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            return json;
        }

        private async Task<string> SendPostRequestAsync(string url, string postData)
        {
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(postData);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                var operation = req.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[VoicevoxClient] VOICEVOX サーバー未検出 ({url}): Result={req.result}, Error={req.error}");
                    return null;
                }

                return req.downloadHandler.text;
            }
        }

        private async Task<byte[]> SendPostBinaryRequestAsync(string url, string jsonBody)
        {
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                var operation = req.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[VoicevoxClient] VOICEVOX synthesis 未検出 ({url}): {req.error}");
                    return null;
                }

                return req.downloadHandler.data;
            }
        }
    }
}
