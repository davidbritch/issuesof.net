﻿using System;
using System.Threading.Tasks;

using IssueDb.Crawling;
using IssueDb.Querying.Syntax;

using IssuesOfDotNet.Data;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.JSInterop;

namespace IssuesOfDotNet.Pages
{
    public sealed partial class Index : IDisposable
    {
        private static readonly string _defaultSearch = "is:open is:issue";

        private string _searchText;
        private int _pageNumber;

        [Inject]
        public IJSRuntime JSRuntime { get; set; }

        [Inject]
        public NavigationManager NavigationManager { get; set; }

        [Inject]
        public IndexService IndexService { get; set; }

        [Inject]
        public IWebHostEnvironment Environment { get; set; }

        [Inject]
        public SearchService SearchService { get; set; }

        public bool IsDevelopment => Environment.IsDevelopment();

        public int PageNumber
        {
            get => _pageNumber;
            set
            {
                if (_pageNumber != value)
                {
                    _pageNumber = value;
                    ChangeUrl();
                }
            }
        }

        public CrawledIssueResults SearchResults { get; private set; } = CrawledIssueResults.Empty;

        protected override void OnInitialized()
        {
            IndexService.Changed += IndexService_Changed;
            NavigationManager.LocationChanged += NavigationManager_LocationChanged;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                var dotNetObjRef = DotNetObjectReference.Create(this);
                await JSRuntime.InvokeVoidAsync("registerPage", dotNetObjRef);
                ApplyQueryParameters();
            }
        }

        public void Dispose()
        {
            IndexService.Changed -= IndexService_Changed;
            NavigationManager.LocationChanged -= NavigationManager_LocationChanged;
        }

        private void IndexService_Changed(object sender, EventArgs e)
        {
            InvokeAsync(() =>
            {
                ApplyQueryParameters();
            });
        }

        private async void NavigationManager_LocationChanged(object sender, LocationChangedEventArgs e)
        {
            var beforeSearchText = _searchText;
            ApplyQueryParameters();
            if (_searchText != beforeSearchText)
                await JSRuntime.InvokeVoidAsync("setCodeMirrorText", _searchText);
        }

        private void ApplyQueryParameters()
        {
            var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
            var parameters = QueryHelpers.ParseQuery(uri.Query);

            if (parameters.TryGetValue("q", out var q))
                _searchText = q;
            else
                _searchText = _defaultSearch;

            if (parameters.TryGetValue("page", out var pageText) && int.TryParse(pageText, out var page))
                _pageNumber = page;
            else
                _pageNumber = 1;

            SearchResults = Find(_searchText);
            StateHasChanged();
        }

        [JSInvokable]
        public void Search(string searchText)
        {
            SearchResults = Find(searchText);
            PageNumber = 1;
            ChangeUrl();
            StateHasChanged();
        }

        private CrawledIssueResults Find(string searchText)
        {
            _searchText = searchText;

            return SearchService.Search(searchText);
        }

        private async void ChangeUrl(bool recordHistory = false)
        {
            var isDefaultQuery = (string.IsNullOrEmpty(_searchText) ||
                                  _searchText.Trim() == _defaultSearch) &&
                                  PageNumber <= 1;

            if (isDefaultQuery)
                return;

            var query = $"?q={Uri.EscapeDataString(_searchText)}";

            if (query.Length > 0 && PageNumber > 1)
                query += $"&page={PageNumber}";

            var uri = new UriBuilder(NavigationManager.Uri)
            {
                Query = query
            }.ToString();

            // Let's update the URL on the client without navigating.
            //
            // NOTE: We want to replace the history state because this is done on every
            //       keystroke in the search box.

            await JSRuntime.InvokeVoidAsync("Blazor.navigateTo",
                                            uri.ToString(),
                                            /* forceLoad */ false,
                                            /* replace */ !recordHistory);
        }

        private void CollapseAll()
        {
            SearchResults.CollapseAll();
            PageNumber = 1;
        }

        private void ExpandAll()
        {
            SearchResults.ExpandAll();
            PageNumber = 1;
        }

        private string GetDownloadLink()
        {
            return $"download/?q={Uri.EscapeDataString(_searchText ?? _defaultSearch)}";
        }

        private async void GoToLabel(string labelName)
        {
            var labelQuery = "label:" + QuerySyntax.EscapeValue(labelName);
            if (!_searchText.Contains(labelQuery))
            {
                SearchResults = Find(_searchText + " " + labelQuery);
                PageNumber = 1;
                ChangeUrl(recordHistory: true);
                await JSRuntime.InvokeVoidAsync("setCodeMirrorText", _searchText);
            }
        }
    }
}
