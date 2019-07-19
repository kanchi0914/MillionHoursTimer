using Toggl.Services;
using Toggl;
using Toggl.Extensions;
using Toggl.Interfaces;
using System.Collections.Generic;
using Toggl.Services;
using Toggl;
using Toggl.Extensions;
using Toggl.Interfaces;
using System;
using System.Linq;
using System.Windows;

namespace WpfApp2
{

    public class TogglManager
    {
        private MainWindow mainWindow;

        public string ApiKey { get; set; }
        string PreApiKey { get; set; } = "";
        public string User{ get; set; } = "";

        TimeEntryService timeEntryService;

        private int defaultWorkspaceID;

        public Dictionary<string, int> ProjectIDs { get; private set; }
        public List<string> Tags { get; set; }

        public TogglManager(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;

            //ApiKey = Properties.Settings.Default.APIKey.ToString();
            ApiKey = Settings.APIKey;

            try
            {
                Init();
            }
            catch(Exception e)
            {
                ApiKey = "";
                Console.WriteLine(e);
            }

        }

        /// <summary>
        /// APIキーを認証
        /// </summary>
        public void Init()
        {

            try
            {
                if (string.IsNullOrEmpty(ApiKey))
                {
                    return;
                }
                
                var userService = new UserService(ApiKey);
                User = userService.GetCurrent().Email;

                ProjectIDs = new Dictionary<string, int>() { { "", 0 } };
                Tags = new List<string>() { "" };

                var projectService = new ProjectService(ApiKey);
                List<Project> projects = projectService.List();
                foreach (Project p in projects)
                {
                    ProjectIDs.Add(p.Name, (int)p.Id);
                }

                timeEntryService = new TimeEntryService(ApiKey);

                var workspaceService = new WorkspaceService(ApiKey);
                List<Workspace> workspaces = workspaceService.List();
                defaultWorkspaceID = workspaces.First().Id.Value;
                var tags = workspaceService.Tags(defaultWorkspaceID);
                foreach (Tag t in tags)
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
        /// テスト用メソッド
        /// </summary>
        public void Test()
        {

            TimeEntry te2 = new TimeEntry()
            {
                IsBillable = true,
                CreatedWith = "TogglAPI.Net",
                Description = "Test",
                ProjectId = 150055033,
                Duration = 1200,
                Start = DateTime.Now.ToIsoDateStr(),
                Stop = DateTime.Now.AddMinutes(10).ToIsoDateStr(),
                WorkspaceId = defaultWorkspaceID
            };

            timeEntryService.Add(te2);
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

            //DateTime d = DateTime.Now.AddHours(-3);

            int duration = (int)(appData.LastTime - appData.LaunchedTime).TotalSeconds;
            TimeEntry te = new TimeEntry()
            {
                IsBillable = true,
                CreatedWith = "TogglAPI.Net",
                Description = appData.DisplayedName,
                Duration = duration,
                Start = appData.LaunchedTime.ToIsoDateStr(),
                Stop = appData.LastTime.ToIsoDateStr(),
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
                    timeEntryService.Add(te);
                }
            }
            catch
            {

            }

        }

    }
}
