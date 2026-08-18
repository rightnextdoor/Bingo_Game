using System;
using System.Text;
using UnityEngine;

[Serializable]
public class NetworkConnectionPayload
{
    #region Fields

    [SerializeField] private int protocolVersion;
    [SerializeField] private string bingoUserId;

    public int ProtocolVersion => protocolVersion;
    public string BingoUserId => bingoUserId;

    #endregion

    #region Constructors

    public NetworkConnectionPayload(int protocolVersion, string bingoUserId)
    {
        this.protocolVersion = protocolVersion;
        this.bingoUserId = bingoUserId;
    }

    #endregion

    #region Serialization

    public byte[] ToBytes()
    {
        string json = JsonUtility.ToJson(this);
        return Encoding.UTF8.GetBytes(json);
    }

    public static bool TryFromBytes(byte[] bytes, out NetworkConnectionPayload payload)
    {
        payload = null;

        if (bytes == null || bytes.Length == 0)
        {
            return false;
        }

        try
        {
            string json = Encoding.UTF8.GetString(bytes);

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            payload = JsonUtility.FromJson<NetworkConnectionPayload>(json);
            return payload != null;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to read network connection payload. {exception.Message}");
            payload = null;
            return false;
        }
    }

    #endregion
}
