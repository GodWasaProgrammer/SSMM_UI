using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SSMM_UI.Messenger;
using System;

namespace SSMM_UI.ViewModel;

public partial class AboutViewModel : ObservableObject
{
    public static string GitLink => "https://github.com/GodWasaProgrammer";
    public static string LinkedInLink => "https://www.linkedin.com/in/bnilssondev/";
    public static string DonationLinks => "";
    public static string YoutubeLink => "https://www.youtube.com/@Cybercolascorner";
    public static string TwitchLink => "https://www.twitch.tv/cybercolagaming";
    public static string KickLink => "https://kick.com/cybercola";
    public static string AboutText => "This software is developed and maintained by Björn Nilsson aka GodWasAProgrammer";

    private static readonly string pathtoProfilePic = "About/ProfilePic.jpeg";

    [RelayCommand]
    public void CloseWindow()
    {
        WeakReferenceMessenger.Default.Send(new CloseWindowMessage
        {
            Sender = new WeakReference(this)
        });
    }

    public Bitmap ProfileImage { get; set; }

    public AboutViewModel()
    {
        ProfileImage = new(pathtoProfilePic);
    }

}
