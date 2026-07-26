using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace EvacuationShelter.Voice
{
    /// <summary>
    /// AI演技＋避難所対話ロールプレイ動作テスト用のUI制御コンポーネント
    /// </summary>
    public class ActingDemoUI : MonoBehaviour
    {
        [Header("対象コントローラー・マネージャー")]
        [SerializeField] private CharacterActingController actingController;
        [SerializeField] private AiDialogueManager aiDialogueManager;

        [Header("API Key 設定")]
        [SerializeField] private InputField apiKeyInputField;
        [SerializeField] private TMP_InputField tmpApiKeyInputField;

        [Header("対話・セリフ入力UI")]
        [SerializeField] private InputField inputTextField;
        [SerializeField] private TMP_InputField tmpInputTextField;

        [Header("ボタン制御")]
        [SerializeField] private Button startSessionButton;
        [SerializeField] private Button playButton;

        [Header("表示UI (文字・状況表示)")]
        [SerializeField] private Text characterNameText;
        [SerializeField] private TextMeshProUGUI tmpCharacterNameText;

        [SerializeField] private Text situationText;
        [SerializeField] private TextMeshProUGUI tmpSituationText;

        [SerializeField] private Text statusText;
        [SerializeField] private TextMeshProUGUI tmpStatusText;

        private void Start()
        {
            if (actingController == null) actingController = FindObjectOfType<CharacterActingController>();
            if (aiDialogueManager == null) aiDialogueManager = FindObjectOfType<AiDialogueManager>();

            if (startSessionButton != null)
            {
                startSessionButton.onClick.AddListener(OnStartSessionButtonClicked);
            }

            if (playButton != null)
            {
                playButton.onClick.AddListener(OnSendButtonClicked);
            }

            if (aiDialogueManager != null)
            {
                aiDialogueManager.OnResponseReceived += HandleAiResponse;
                aiDialogueManager.OnStatusChanged += UpdateStatus;

                // 保存されたキーがあれば入力欄に自動セット
                if (!string.IsNullOrEmpty(aiDialogueManager.ApiKey))
                {
                    if (tmpApiKeyInputField != null) tmpApiKeyInputField.text = aiDialogueManager.ApiKey;
                    if (apiKeyInputField != null) apiKeyInputField.text = aiDialogueManager.ApiKey;
                }
            }

            UpdateStatus("準備完了。Gemini API Key を設定して「体験モード開始」を押してください。");
        }

        private async void OnStartSessionButtonClicked()
        {
            ApplyApiKey();

            if (aiDialogueManager == null)
            {
                UpdateStatus("エラー: AiDialogueManager が見つかりません。");
                return;
            }

            SetButtonsInteractable(false);
            UpdateStatus("体験モードを初期化中... キャラクターを選出しています。");

            try
            {
                await aiDialogueManager.StartSessionAsync();
            }
            catch (Exception ex)
            {
                UpdateStatus($"エラー: {ex.Message}");
            }
            finally
            {
                SetButtonsInteractable(true);
            }
        }

        private async void OnSendButtonClicked()
        {
            ApplyApiKey();

            string userMessage = GetInputText();
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                UpdateStatus("入力欄にメッセージを入れてください。");
                return;
            }

            if (aiDialogueManager == null)
            {
                UpdateStatus("エラー: AiDialogueManager が見つかりません。");
                return;
            }

            SetButtonsInteractable(false);

            try
            {
                await aiDialogueManager.SendUserMessageAsync(userMessage);
                ClearInputText();
            }
            catch (Exception ex)
            {
                UpdateStatus($"エラー: {ex.Message}");
            }
            finally
            {
                SetButtonsInteractable(true);
            }
        }

        private void HandleAiResponse(AiRoleplayResponse response)
        {
            if (response == null) return;

            // キャラクター名の更新
            string nameStr = $"【{response.characterId}】 {response.characterName}";
            if (tmpCharacterNameText != null) tmpCharacterNameText.text = nameStr;
            if (characterNameText != null) characterNameText.text = nameStr;

            // 状況描写の更新
            string sitStr = response.situation;
            if (tmpSituationText != null) tmpSituationText.text = sitStr;
            if (situationText != null) situationText.text = sitStr;
        }

        private void ApplyApiKey()
        {
            if (aiDialogueManager == null) return;

            string key = "";
            if (tmpApiKeyInputField != null && !string.IsNullOrWhiteSpace(tmpApiKeyInputField.text))
                key = tmpApiKeyInputField.text;
            else if (apiKeyInputField != null && !string.IsNullOrWhiteSpace(apiKeyInputField.text))
                key = apiKeyInputField.text;

            if (!string.IsNullOrEmpty(key))
            {
                aiDialogueManager.ApiKey = key;
            }
        }

        private string GetInputText()
        {
            if (tmpInputTextField != null && !string.IsNullOrWhiteSpace(tmpInputTextField.text))
                return tmpInputTextField.text;
            if (inputTextField != null && !string.IsNullOrWhiteSpace(inputTextField.text))
                return inputTextField.text;
            return string.Empty;
        }

        private void ClearInputText()
        {
            if (tmpInputTextField != null) tmpInputTextField.text = "";
            if (inputTextField != null) inputTextField.text = "";
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (startSessionButton != null) startSessionButton.interactable = interactable;
            if (playButton != null) playButton.interactable = interactable;
        }

        private void UpdateStatus(string message)
        {
            if (tmpStatusText != null) tmpStatusText.text = message;
            if (statusText != null) statusText.text = message;
            Debug.Log($"[ActingDemoUI] {message}");
        }
    }
}
