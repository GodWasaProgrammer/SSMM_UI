using SSMM_UI.Enums;
using SSMM_UI.RTMP;
using SSMM_UI.Settings;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace SSMM_UI.Interfaces;

public interface IDialogService
{
    Task<bool> ShowServerDetailsAsync(RtmpServiceGroup group);
    Task<UserSettings> ShowSettingsDialogAsync(UserSettings currentSettings);
    Task About();
    Task InspectSelectedService(SelectedService value);
    Task WebhooksView();
    Task DeleteToken(AuthProvider provider, bool result);
    Task DeleteAllTokens(bool result);
    Task PurgeSpecificToken();
}