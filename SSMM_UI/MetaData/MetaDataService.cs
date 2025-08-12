using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using SSMM_UI.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SSMM_UI.MetaData;

public class MetaDataService
{
    private YouTubeService _youTubeService;
    private List<VideoCategory> _ytCategories;
    public IReadOnlyList<VideoCategory> YouTubeCategories => _ytCategories;

    public MetaDataService(YouTubeService service)
    {
        _youTubeService = service;
        _ytCategories = [];
    }

    public async Task Initialize()
    {
        await YTCategoryFetch();
    }

    private async Task YTCategoryFetch()
    {
        var categoriesRequest = _youTubeService.VideoCategories.List("snippet");
        categoriesRequest.RegionCode = "SE"; // Eller "US", "GB", etc.

        try
        {
            var result = await categoriesRequest.ExecuteAsync();
            if (result is not null)
            {

                VideoCategoryListResponse response = new();
                response = result;
                _ytCategories = [];
                foreach (var category in new VideoCategoryListResponse().Items)
                {
                    if (category.Snippet.Assignable == true)
                    {
                        LogService.Log($"{category.Id} - {category.Snippet.Title}");
                        _ytCategories.Add(category);
                    }
                }
            }
            else
            {
                throw (new Exception("API call to YT failed to fetch categories"));
            }
        }
        catch (Exception ex)
        {
            LogService.Log(ex.Message);
        }
    }

    private static void TwitchCategoryFetch()
    {

    }
    // TODO: API Support missing, need to be haxxy
    private static void YTGameFetch()
    {

    }

}
