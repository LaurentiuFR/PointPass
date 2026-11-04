using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace PointPass.Online
{
    /// <summary>Bounded requests using platform certificate validation; no certificate overrides.</summary>
    public static class OnlineHttp
    {
        public static IEnumerator Post<T>(string endpoint, object payload, string bearer, string workerKey,
            Action<T, string> completed, int timeout = 8) where T : class
        {
            using (var request = new UnityWebRequest(endpoint, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = timeout;
                request.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(bearer)) request.SetRequestHeader("Authorization", "Bearer " + bearer);
                if (!string.IsNullOrEmpty(workerKey)) request.SetRequestHeader("X-PointPass-Worker-Key", workerKey);
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    string error = request.responseCode == 0 ? "Connection failed. Check your internet connection and try again." : "The server rejected this request.";
                    if (request.downloadedBytes <= 8192 && request.responseCode > 0)
                    {
                        OnlineApiContracts.Error detail = null;
                        try { detail = JsonUtility.FromJson<OnlineApiContracts.Error>(request.downloadHandler.text); }
                        catch (ArgumentException) { }
                        if (detail != null && !string.IsNullOrWhiteSpace(detail.message) && detail.message.Length <= 256) error = detail.message;
                    }
                    completed(null, error);
                }
                else if (request.responseCode == 204) completed(null, null);
                else if (request.downloadedBytes > 8192) completed(null, "Server response exceeded the pilot limit.");
                else
                {
                    T result = null;
                    try { result = JsonUtility.FromJson<T>(request.downloadHandler.text); }
                    catch (ArgumentException) { }
                    completed(result, result == null ? "The server returned an invalid response." : null);
                }
            }
        }
    }
}
