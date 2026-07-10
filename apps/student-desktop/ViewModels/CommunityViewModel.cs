using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentDesktop.Models;
using StudentDesktop.Services;

namespace StudentDesktop.ViewModels;

// SDA-16: view and post in class, subject-section, and club groups; material shared in a
// group surfaces in that group's Materials list without a separate upload step (reads the
// same rows TWA-06's upload endpoint writes).
public partial class CommunityViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;

    public ObservableCollection<GroupDto> Groups { get; } = [];
    public ObservableCollection<GroupPostDto> Posts { get; } = [];
    public ObservableCollection<MaterialDto> Materials { get; } = [];

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private GroupDto? _selectedGroup;

    [ObservableProperty]
    private string _newPostContent = "";

    public CommunityViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
        _ = LoadGroupsAsync();
    }

    [RelayCommand]
    private async Task LoadGroupsAsync()
    {
        try
        {
            var groups = await _apiClient.GetMyGroupsAsync();
            Groups.Clear();
            foreach (var group in groups)
            {
                Groups.Add(group);
            }
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = "Could not reach the server. Check your connection and try again.";
        }
    }

    partial void OnSelectedGroupChanged(GroupDto? value) => _ = LoadGroupContentAsync(value);

    private async Task LoadGroupContentAsync(GroupDto? group)
    {
        Posts.Clear();
        Materials.Clear();
        if (group is null)
        {
            return;
        }
        try
        {
            var posts = await _apiClient.GetGroupPostsAsync(group.Id);
            foreach (var post in posts)
            {
                Posts.Add(post);
            }
            var materials = await _apiClient.GetGroupMaterialsAsync(group.Id);
            foreach (var material in materials)
            {
                Materials.Add(material);
            }
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = "Could not reach the server. Check your connection and try again.";
        }
    }

    [RelayCommand]
    private async Task PostAsync()
    {
        if (SelectedGroup is null || string.IsNullOrWhiteSpace(NewPostContent))
        {
            return;
        }
        try
        {
            var post = await _apiClient.CreateGroupPostAsync(SelectedGroup.Id, NewPostContent.Trim());
            Posts.Insert(0, post);
            NewPostContent = "";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = "Could not reach the server. Check your connection and try again.";
        }
    }
}
