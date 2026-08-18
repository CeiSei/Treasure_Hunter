using Unity.Netcode.Components;
using UnityEngine;

[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    // 서버 권한을 해제하고 클라이언트 권한으로 변경합니다.
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}