using Android.App;
using Android.Content;
using Android.Content.PM;
using Microsoft.Maui;

namespace RetroShite;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
                           ConfigChanges.UiMode | ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
    ScreenOrientation = ScreenOrientation.Portrait)]
public class MainActivity : MauiAppCompatActivity
{
#if RETROSHITE_LEGACY
    private const int SaveZipRequest = 4071;
    private static TaskCompletionSource<Android.Net.Uri?>? _pendingSave;

    public static Task<Android.Net.Uri?> ChooseExportDestinationAsync(string filename)
    {
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity as MainActivity
            ?? throw new InvalidOperationException("The Android activity is not available.");
        if (_pendingSave is not null)
            throw new InvalidOperationException("A file selection is already in progress.");

        var pending = new TaskCompletionSource<Android.Net.Uri?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingSave = pending;
        try
        {
            var intent = new Intent(Intent.ActionCreateDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("application/zip");
            intent.PutExtra(Intent.ExtraTitle, filename);
            activity.StartActivityForResult(intent, SaveZipRequest);
        }
        catch (Exception ex)
        {
            _pendingSave = null;
            pending.TrySetException(ex);
        }
        return pending.Task;
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != SaveZipRequest) return;
        var pending = _pendingSave;
        _pendingSave = null;
        if (resultCode == Result.Ok)
            pending?.TrySetResult(data?.Data);
        else
            pending?.TrySetResult(null);
    }
#endif
}
