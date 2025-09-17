using SSMM_UI.RTMP;
using SSMM_UI.Settings;
using System.Threading.Tasks;

namespace SSMM_UI.Interfaces;

public interface IDialogService
{
    Task<bool> ShowServerDetailsAsync(RtmpServiceGroup group);
    Task<UserSettings> ShowSettingsDialogAsync(UserSettings currentSettings);
    Task About();
}
