using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
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
        _ytCategories = new List<VideoCategory>();
    }

    public async Task Initialize()
    {
        await YTCategoryFetch();
    }

    private async Task YTCategoryFetch()
    {
        var categoriesRequest = _youTubeService.VideoCategories.List("snippet");
        categoriesRequest.RegionCode = "SE"; // Eller "US", "GB", etc.

        VideoCategoryListResponse response = new();
        try
        {
            var result = await categoriesRequest.ExecuteAsync();
            if (result is not null)
            {
                response = result;
                _ytCategories = new List<VideoCategory>();
                foreach (var category in response.Items)
                {
                    if (category.Snippet.Assignable == true)
                    {
                        Console.WriteLine($"{category.Id} - {category.Snippet.Title}");
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
            Console.WriteLine(ex.Message);
        }
    }

    private async Task TwitchCategoryFetch()
    {

    }
    // TODO: API Support missing, need to be haxxy
    private async Task YTGameFetch()
    {

    }

}
