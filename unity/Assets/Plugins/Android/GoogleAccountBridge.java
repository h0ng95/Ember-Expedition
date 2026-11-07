package com.ignis.account;

import android.app.Activity;
import android.os.CancellationSignal;
import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.ClearCredentialStateRequest;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialException;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.ClearCredentialException;
import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;
import com.unity3d.player.UnityPlayer;
import org.json.JSONObject;

public final class GoogleAccountBridge {
    private static CancellationSignal cancellation;
    private GoogleAccountBridge() { }
    public static void signIn(String webClientId, String target, String requestId) {
        Activity activity = UnityPlayer.currentActivity;
        activity.runOnUiThread(() -> {
            try {
                if (cancellation != null) cancellation.cancel();
                cancellation = new CancellationSignal();
                GetSignInWithGoogleOption option = new GetSignInWithGoogleOption.Builder(webClientId).build();
                GetCredentialRequest request = new GetCredentialRequest.Builder().addCredentialOption(option).build();
                CredentialManager.create(activity).getCredentialAsync(activity, request, cancellation,
                    command -> activity.runOnUiThread(command), new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                    @Override public void onResult(GetCredentialResponse result) {
                        try {
                            Credential credential = result.getCredential();
                            if (!(credential instanceof CustomCredential) ||
                                !GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(credential.getType())) {
                                send(target, requestId, null, "unsupported"); return;
                            }
                            String token = GoogleIdTokenCredential.createFrom(credential.getData()).getIdToken();
                            send(target, requestId, token, null);
                        } catch (Exception exception) { send(target, requestId, null, "invalid_response"); }
                    }
                    @Override public void onError(GetCredentialException error) {
                        send(target, requestId, null, error instanceof GetCredentialCancellationException ? "cancelled" : "unavailable");
                    }
                });
            } catch (Exception exception) { send(target, requestId, null, "unavailable"); }
        });
    }
    private static void send(String target, String requestId, String token, String error) {
        try {
            JSONObject result = new JSONObject(); result.put("requestId", requestId);
            if (token != null) result.put("token", token);
            if (error != null) result.put("error", error);
            UnityPlayer.UnitySendMessage(target, "OnGoogleAccountResult", result.toString());
        } catch (Exception ignored) { }
    }
    public static void cancel() { if (cancellation != null) { cancellation.cancel(); cancellation = null; } }
    public static void clearSession() {
        cancel();
        Activity activity = UnityPlayer.currentActivity;
        activity.runOnUiThread(() -> CredentialManager.create(activity).clearCredentialStateAsync(
            new ClearCredentialStateRequest(), null, command -> activity.runOnUiThread(command),
            new CredentialManagerCallback<Void, ClearCredentialException>() {
                @Override public void onResult(Void unused) { }
                @Override public void onError(ClearCredentialException error) { }
            }));
    }
}
