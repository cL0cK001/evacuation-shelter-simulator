#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace EvacuationShelter.Voice
{
    /// <summary>
    /// Unityエディタ上でワンクリックで「避難所AI対話デモUI」を自動生成するセットアップスクリプト
    /// </summary>
    public static class AutoSetupShelterUI
    {
        [MenuItem("Tools/避難所AIデモUIを自動生成")]
        public static void SetupUI()
        {
            // 既存の Canvas があるか確認、なければ作成
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // EventSystem の確認
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // メイン管理オブジェクト
            GameObject managerObj = GameObject.Find("ShelterAIManager");
            if (managerObj == null)
            {
                managerObj = new GameObject("ShelterAIManager");
            }

            AudioSource audioSource = managerObj.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = managerObj.AddComponent<AudioSource>();

            VoicevoxClient voicevox = managerObj.GetComponent<VoicevoxClient>();
            if (voicevox == null) voicevox = managerObj.AddComponent<VoicevoxClient>();

            CharacterActingController actingController = managerObj.GetComponent<CharacterActingController>();
            if (actingController == null) actingController = managerObj.AddComponent<CharacterActingController>();

            AiDialogueManager dialogueManager = managerObj.GetComponent<AiDialogueManager>();
            if (dialogueManager == null) dialogueManager = managerObj.AddComponent<AiDialogueManager>();

            ActingDemoUI demoUI = managerObj.GetComponent<ActingDemoUI>();
            if (demoUI == null) demoUI = managerObj.AddComponent<ActingDemoUI>();

            // UI パネルの作成
            GameObject panelObj = new GameObject("AIPanel", typeof(RectTransform), typeof(Image));
            panelObj.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.05f, 0.05f);
            panelRect.anchorMax = new Vector2(0.95f, 0.95f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelObj.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            // API Key 入力欄
            InputField apiKeyInput = CreateInputField(panelObj.transform, "ApiKeyInput", "Gemini API Key を入力...", new Vector2(0, 180), new Vector2(600, 40));

            // 体験モード開始ボタン
            Button startBtn = CreateButton(panelObj.transform, "StartSessionButton", "体験モード開始", new Vector2(-150, 120), new Vector2(250, 50));

            // キャラクター名表示 Text
            Text charNameText = CreateText(panelObj.transform, "CharacterNameText", "【選択前】体験モードを開始してください", 20, FontStyle.Bold, new Vector2(0, 50), new Vector2(700, 40));

            // 状況描写 Text
            Text situationText = CreateText(panelObj.transform, "SituationText", "【状況】ここにAIの仕草や行動が表示されます。", 16, FontStyle.Italic, new Vector2(0, -10), new Vector2(700, 60));

            // メッセージ入力欄
            InputField messageInput = CreateInputField(panelObj.transform, "MessageInput", "専門家としてメッセージを入力...", new Vector2(-100, -100), new Vector2(450, 45));

            // 送信ボタン
            Button sendBtn = CreateButton(panelObj.transform, "SendButton", "送信", new Vector2(200, -100), new Vector2(120, 45));

            // ステータス表示 Text
            Text statusText = CreateText(panelObj.transform, "StatusText", "準備完了", 14, FontStyle.Normal, new Vector2(0, -170), new Vector2(700, 30));

            // SerializedObject で ActingDemoUI に自動参照バインド
            SerializedObject serializedUI = new SerializedObject(demoUI);
            serializedUI.FindProperty("actingController").objectReferenceValue = actingController;
            serializedUI.FindProperty("aiDialogueManager").objectReferenceValue = dialogueManager;
            serializedUI.FindProperty("apiKeyInputField").objectReferenceValue = apiKeyInput;
            serializedUI.FindProperty("inputTextField").objectReferenceValue = messageInput;
            serializedUI.FindProperty("startSessionButton").objectReferenceValue = startBtn;
            serializedUI.FindProperty("playButton").objectReferenceValue = sendBtn;
            serializedUI.FindProperty("characterNameText").objectReferenceValue = charNameText;
            serializedUI.FindProperty("situationText").objectReferenceValue = situationText;
            serializedUI.FindProperty("statusText").objectReferenceValue = statusText;
            serializedUI.ApplyModifiedProperties();

            Selection.activeGameObject = managerObj;
            Debug.Log("<color=green>[AutoSetupShelterUI] 避難所AIデモUIの自動生成とバインドが完了しました！</color>");
        }

        private static InputField CreateInputField(Transform parent, string name, string placeholderText, Vector2 anchoredPos, Vector2 size)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            obj.GetComponent<Image>().color = Color.white;

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(obj.transform, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.sizeDelta = Vector2.zero;
            Text text = textObj.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.color = Color.black;

            GameObject placeholderObj = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            placeholderObj.transform.SetParent(obj.transform, false);
            RectTransform phRect = placeholderObj.GetComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero; phRect.anchorMax = Vector2.one; phRect.sizeDelta = Vector2.zero;
            Text phText = placeholderObj.GetComponent<Text>();
            phText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            phText.fontSize = 16;
            phText.fontStyle = FontStyle.Italic;
            phText.color = Color.gray;
            phText.text = placeholderText;

            InputField inputField = obj.GetComponent<InputField>();
            inputField.textComponent = text;
            inputField.placeholder = phText;

            return inputField;
        }

        private static Button CreateButton(Transform parent, string name, string labelText, Vector2 anchoredPos, Vector2 size)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            obj.GetComponent<Image>().color = new Color(0.2f, 0.6f, 1.0f);

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(obj.transform, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.sizeDelta = Vector2.zero;
            Text text = textObj.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = labelText;

            return obj.GetComponent<Button>();
        }

        private static Text CreateText(Transform parent, string name, string content, int fontSize, FontStyle style, Vector2 anchoredPos, Vector2 size)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            Text text = obj.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = content;

            return text;
        }
    }
}
#endif
