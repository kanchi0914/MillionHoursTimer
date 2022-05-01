using Toggl.Api.Services;
using Toggl.Api.DataObjects;
using System.Collections.Generic;
using System;
using System.Linq;
using Toggl.Api.Extensions;

namespace MHTimer
{

    public class TogglManager
    {
        private MainWindow mainWindow;

        public string ApiKey { get; set; }
        string PreApiKey { get; set; } = "";
        public string User{ get; set; } = "";

        TimeEntryServiceAsync timeEntryService;

        private long defaultWorkspaceID;

        public Dictionary<string, int> ProjectIDs { get; private set; }
        public List<string> Tags { get; set; }

        public TogglManager(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            ApiKey = Settings.APIKey;

            try
            {
                Init();
            }
            catch(Exception ex)
            {
                ApiKey = "";
                ErrorLogger.Log(ex);
            }

        }

        /// <summary>
        /// APIキーを認証
        /// </summary>
        public void Init()
        {

            ProjectIDs = new Dictionary<string, int>() { { "", 0 } };
            Tags = new List<string>() { "" };

            try
            {
                if (string.IsNullOrEmpty(ApiKey))
                {
                    return;
                }

                var userService = new UserServiceAsync(ApiKey);
                User = userService.GetCurrentAsync().Result.Email;
                var projectService = new ProjectServiceAsync(ApiKey);
                var projects = projectService.ListAsync().Result;

                foreach (var p in projects)
                {
                    ProjectIDs.Add(p.Name, (int)p.Id);
                }

                timeEntryService = new TimeEntryServiceAsync(ApiKey);

                var workspaceService = new WorkspaceServiceAsync(ApiKey);
                var workspaces = workspaceService.GetAllAsync();
                defaultWorkspaceID = workspaces.Result.First().Id;
                var tags = workspaceService.GetTagsAsync(defaultWorkspaceID).Result;
                foreach (var t in tags)
                {
                    Tags.Add(t.Name);
                }

            }
            catch (Exception e)
            {
                throw e;
            }

        }

        /// <summary>
        /// APIキーを設定し，アカウント情報を更新
        /// </summary>
        /// <param name="key"></param>
        public void SetAPIKey(string key)
        {
            try
            {
                ApiKey = key;
                Init();
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// TimeEntryを追加
        /// </summary>
        /// <param name="appData"></param>
        public void SetTimeEntry(AppDataObject appData)
        {

            int duration = (int)(appData.LastRunningTime - appData.LaunchedTime).TotalSeconds;
            var te = new TimeEntry()
            {
                IsBillable = true,
                CreatedWith = "TogglAPI.Net",
                Description = appData.DisplayedName,
                Duration = duration,
                Start = appData.LaunchedTime.ToIsoDateStr(),
                Stop = appData.LastRunningTime.ToIsoDateStr(),
                WorkspaceId = defaultWorkspaceID
            };

            if (ProjectIDs.ContainsKey(appData.LinkedProjectName))
            {
                te.ProjectId = ProjectIDs[appData.LinkedProjectName];
            }

            if (!string.IsNullOrEmpty(appData.LinkedTag))
            {
                te.TagNames = new List<string>() { appData.LinkedTag };
            }

            try
            {
                if (te != null)
                {
                    timeEntryService.CreateAsync(te);
                    Console.WriteLine($"sended:{te}");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex);
            }

        }

    }
}
