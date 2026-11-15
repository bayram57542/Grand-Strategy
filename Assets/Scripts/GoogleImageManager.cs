using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace GrandStrategy.Core
{
    /// <summary>
    /// Google AI Studio (Imagen / Gemini Image Generation REST API) vasitəsilə
    /// mətn (prompt) əsasında 2D oyun qrafikləri və ikonları yaradan Singleton menecer.
    /// </summary>
    public class GoogleImageManager : MonoBehaviour
    {
        private static GoogleImageManager _instance;
        public static GoogleImageManager Instance => _instance != null ? _instance : (_instance = FindAnyObjectByType<GoogleImageManager>());

        public const string DefaultProvinceIconPrompt = "medieval strategy game province icon, 2D UI element, transparent background";

        [Header("Google AI Studio API Tənzimləmələri")]
        [Tooltip("Google AI Studio (https://aistudio.google.com/apikey) üzərindən aldığınız API açarını bura yapışdırın.")]
        [SerializeField] private string apiKey = "";

        [Tooltip("Əsas Imagen model adı (məs: imagen-3.0-generate-002 və ya imagen-4.0-generate-001)")]
        [SerializeField] private string imagenModel = "imagen-3.0-generate-002";

        [Tooltip("Əgər hesabda Imagen :predict aktiv deyilsə, avtomatik istifadə olunacaq Gemini Image modeli")]
        [SerializeField] private string fallbackGeminiImageModel = "gemini-2.5-flash-image";

        [Tooltip("Sorğu gözləmə müddəti (saniyə)")]
        [SerializeField] private int requestTimeoutSeconds = 45;

        private bool _isRequestInProgress;
        private Texture2D _lastGeneratedTexture;

        public bool IsRequestInProgress => _isRequestInProgress;
        public bool HasApiKey => !string.IsNullOrWhiteSpace(apiKey);
        public Texture2D LastGeneratedTexture => _lastGeneratedTexture;

        public event Action<string> OnGenerationStarted;
        public event Action<Texture2D> OnGenerationSucceeded;
        public event Action<string> OnGenerationFailed;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        /// <summary>
        /// Verilmiş prompt əsasında Google AI Studio REST API-sinə sorğu göndərir
        /// və nəticəni Texture2D olaraq qaytarır.
        /// </summary>
        public void GenerateImage(
            string prompt = DefaultProvinceIconPrompt,
            Action<Texture2D> onSuccess = null,
            Action<string> onError = null)
        {
            if (_isRequestInProgress)
            {
                const string busyMsg = "[GoogleImageManager] Hazırda şəkil generasiyası sorğusu icra olunur. Zəhmət olmasa gözləyin.";
                Debug.LogWarning(busyMsg);
                onError?.Invoke(busyMsg);
                return;
            }

            string effectivePrompt = string.IsNullOrWhiteSpace(prompt) ? DefaultProvinceIconPrompt : prompt.Trim();
            StartCoroutine(GenerateImageCoroutine(effectivePrompt, onSuccess, onError));
        }

        /// <summary>
        /// Coroutine vasitəsilə Google AI Studio Imagen (:predict) və ehtiyat Gemini Image (:generateContent)
        /// REST API sorğusunu icra edir.
        /// </summary>
        public IEnumerator GenerateImageCoroutine(
            string prompt,
            Action<Texture2D> onSuccess = null,
            Action<string> onError = null)
        {
            string trimmedKey = apiKey != null ? apiKey.Trim() : string.Empty;
            if (string.IsNullOrEmpty(trimmedKey))
            {
                const string noKeyMsg = "API açarı daxil edilməyib! Zəhmət olmasa Hierarchy-də GoogleImageManager obyektini seçib Inspector-da 'Api Key' xanasına Google AI Studio açarınızı yapışdırın.";
                Debug.LogWarning($"[GoogleImageManager] {noKeyMsg}");
                OnGenerationFailed?.Invoke(noKeyMsg);
                onError?.Invoke(noKeyMsg);
                yield break;
            }

            _isRequestInProgress = true;
            OnGenerationStarted?.Invoke(prompt);
            Debug.Log($"[GoogleImageManager] Google AI Studio API sorğusu göndərilir... Prompt: \"{prompt}\" | Model: {imagenModel}");

            string base64Image = null;
            string errorDetails = null;

            // 1. Seçilmiş modelin adına görə ilk uyğun endpoint-i müəyyən edirik
            bool preferGenerateContent = imagenModel.StartsWith("gemini-", StringComparison.OrdinalIgnoreCase);

            if (!preferGenerateContent)
            {
                // Addım 1A: Google Imagen (:predict) REST sorğusu
                string predictUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{imagenModel}:predict?key={trimmedKey}";
                string predictJson = BuildImagenPredictRequestJson(prompt);

                yield return SendPostRequest(
                    predictUrl,
                    predictJson,
                    responseText =>
                    {
                        base64Image = ExtractBase64FromResponse(responseText);
                        if (string.IsNullOrEmpty(base64Image))
                        {
                            errorDetails = $"Imagen cavabında şəkil tapılmadı: {Truncate(responseText, 240)}";
                        }
                    },
                    err => errorDetails = err);

                // Əgər :predict uğursuz oldusa və fallback model varsa, Gemini Image (:generateContent) ilə yenidən cəhd edirik
                if (string.IsNullOrEmpty(base64Image) && !string.IsNullOrWhiteSpace(fallbackGeminiImageModel))
                {
                    Debug.Log($"[GoogleImageManager] Imagen ':predict' cavab vermədi ({errorDetails}). Ehtiyat model ({fallbackGeminiImageModel}) ilə cəhd edilir...");
                    string fallbackUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{fallbackGeminiImageModel}:generateContent?key={trimmedKey}";
                    string fallbackJson = BuildGeminiImageGenerateContentJson(prompt);

                    yield return SendPostRequest(
                        fallbackUrl,
                        fallbackJson,
                        responseText =>
                        {
                            base64Image = ExtractBase64FromResponse(responseText);
                            if (string.IsNullOrEmpty(base64Image))
                            {
                                errorDetails = $"Gemini Image cavabında şəkil tapılmadı: {Truncate(responseText, 240)}";
                            }
                        },
                        err => errorDetails = err);
                }
            }
            else
            {
                // Birbaşa Gemini :generateContent modeli seçilibsə
                string generateUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{imagenModel}:generateContent?key={trimmedKey}";
                string generateJson = BuildGeminiImageGenerateContentJson(prompt);

                yield return SendPostRequest(
                    generateUrl,
                    generateJson,
                    responseText =>
                    {
                        base64Image = ExtractBase64FromResponse(responseText);
                        if (string.IsNullOrEmpty(base64Image))
                        {
                            errorDetails = $"Cavabda şəkil tapılmadı: {Truncate(responseText, 240)}";
                        }
                    },
                    err => errorDetails = err);
            }

            _isRequestInProgress = false;

            if (string.IsNullOrEmpty(base64Image))
            {
                string finalErr = $"[GoogleImageManager] Şəkil generasiya edilə bilmədi: {errorDetails}";
                Debug.LogError(finalErr);
                OnGenerationFailed?.Invoke(finalErr);
                onError?.Invoke(finalErr);
                yield break;
            }

            if (TryDecodeBase64ToTexture(base64Image, out Texture2D generatedTexture, out string decodeError))
            {
                _lastGeneratedTexture = generatedTexture;
                Debug.Log($"[GoogleImageManager] Şəkil uğurla yaradıldı! Ölçü: {generatedTexture.width}x{generatedTexture.height}");
                OnGenerationSucceeded?.Invoke(generatedTexture);
                onSuccess?.Invoke(generatedTexture);
            }
            else
            {
                string finalErr = $"[GoogleImageManager] Base64 şəkli Texture2D-yə çevrilərkən xəta: {decodeError}";
                Debug.LogError(finalErr);
                OnGenerationFailed?.Invoke(finalErr);
                onError?.Invoke(finalErr);
            }
        }

        private IEnumerator SendPostRequest(
            string url,
            string jsonPayload,
            Action<string> onSuccess,
            Action<string> onError)
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = Mathf.Max(10, requestTimeoutSeconds);

            yield return request.SendWebRequest();

            string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;

            if (request.result == UnityWebRequest.Result.Success && request.responseCode >= 200 && request.responseCode < 300)
            {
                onSuccess?.Invoke(responseBody);
            }
            else
            {
                string errSummary = $"HTTP {request.responseCode} ({request.error}) - {Truncate(responseBody, 260)}";
                onError?.Invoke(errSummary);
            }
        }

        /// <summary>
        /// Google Imagen (:predict) REST formatına uyğun JSON sorğusu qurur.
        /// </summary>
        public static string BuildImagenPredictRequestJson(string prompt)
        {
            string escapedPrompt = EscapeJsonString(prompt);
            return $"{{\"instances\":[{{\"prompt\":\"{escapedPrompt}\"}}],\"parameters\":{{\"sampleCount\":1,\"outputOptions\":{{\"mimeType\":\"image/png\"}}}}}}";
        }

        /// <summary>
        /// Google Gemini Image Generation (:generateContent) REST formatına uyğun JSON sorğusu qurur.
        /// </summary>
        public static string BuildGeminiImageGenerateContentJson(string prompt)
        {
            string escapedPrompt = EscapeJsonString(prompt);
            return $"{{\"contents\":[{{\"parts\":[{{\"text\":\"{escapedPrompt}\"}}]}}],\"generationConfig\":{{\"responseModalities\":[\"TEXT\",\"IMAGE\"]}}}}";
        }

        /// <summary>
        /// Həm Imagen (:predict -> "bytesBase64Encoded"), həm də Gemini (:generateContent -> "inlineData":{"data":...})
        /// JSON cavablarından Base64 şəkil sətirini çıxarır.
        /// </summary>
        public static string ExtractBase64FromResponse(string jsonResponse)
        {
            if (string.IsNullOrWhiteSpace(jsonResponse))
            {
                return null;
            }

            // 1. Imagen :predict formatı -> "bytesBase64Encoded": "..."
            string bytesB64 = ExtractJsonStringField(jsonResponse, "bytesBase64Encoded");
            if (!string.IsNullOrEmpty(bytesB64))
            {
                return bytesB64;
            }

            // 2. Gemini :generateContent formatı -> "inlineData": { ... "data": "..." } və ya "inline_data"
            int inlineIdx = jsonResponse.IndexOf("inlineData", StringComparison.OrdinalIgnoreCase);
            if (inlineIdx < 0)
            {
                inlineIdx = jsonResponse.IndexOf("inline_data", StringComparison.OrdinalIgnoreCase);
            }

            if (inlineIdx >= 0)
            {
                string sub = jsonResponse.Substring(inlineIdx);
                string inlineDataB64 = ExtractJsonStringField(sub, "data");
                if (!string.IsNullOrEmpty(inlineDataB64))
                {
                    return inlineDataB64;
                }
            }

            return null;
        }

        /// <summary>
        /// Base64 formatındakı PNG/JPEG baytlarını Unity Texture2D obyektinə çevirir.
        /// </summary>
        public static bool TryDecodeBase64ToTexture(string base64Data, out Texture2D texture, out string error)
        {
            texture = null;
            error = null;

            if (string.IsNullOrWhiteSpace(base64Data))
            {
                error = "Base64 məlumatı boşdur.";
                return false;
            }

            try
            {
                string cleaned = base64Data.Trim()
                    .Replace("\\n", string.Empty)
                    .Replace("\\r", string.Empty)
                    .Replace("\n", string.Empty)
                    .Replace("\r", string.Empty);

                byte[] imageBytes = Convert.FromBase64String(cleaned);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(tex, imageBytes))
                {
                    DestroyImmediate(tex);
                    error = "ImageConversion.LoadImage baytları oxuya bilmədi.";
                    return false;
                }

                tex.name = "GoogleImagen_GeneratedTexture";
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                texture = tex;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string ExtractJsonStringField(string json, string fieldName)
        {
            string pattern = $"\"{fieldName}\"";
            int keyIndex = json.IndexOf(pattern, StringComparison.Ordinal);
            if (keyIndex < 0)
            {
                return null;
            }

            int colonIndex = json.IndexOf(':', keyIndex + pattern.Length);
            if (colonIndex < 0)
            {
                return null;
            }

            int startQuote = json.IndexOf('"', colonIndex + 1);
            if (startQuote < 0)
            {
                return null;
            }

            var sb = new StringBuilder();
            bool escaped = false;
            for (int i = startQuote + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (escaped)
                {
                    sb.Append(c);
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    return sb.ToString();
                }
                else
                {
                    sb.Append(c);
                }
            }

            return null;
        }

        private static string EscapeJsonString(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength) + "...";
        }
    }
}
