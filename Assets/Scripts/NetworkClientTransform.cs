using Unity.Netcode.Components;

public class NetworkClientTransform : NetworkTransform
{
	protected override bool OnIsServerAuthoritative()
	{
		return false;
	}

}
