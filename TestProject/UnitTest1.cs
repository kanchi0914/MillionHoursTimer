using NUnit.Framework;
using System;
using Toggl.Api.Services;
using System.Linq;
using Toggl.Api.Extensions;


namespace Tests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test1()
        {
            ToggleTest();
        }
        
        /// <summary>
        /// テスト用メソッド
        /// </summary>
        public void ToggleTest()
        {
            var apiKey = "8416a58fcdcdb8c4bfcdd60917241a5a";
            TimeEntryServiceAsync timeEntryService = new TimeEntryServiceAsync(apiKey);
            var projectService = new ProjectServiceAsync(apiKey);
            var projects = projectService.ListAsync().Result;
            var projectId = projects.First().Id;
            var workspaceService = new WorkspaceServiceAsync(apiKey);
            var workspaces = workspaceService.GetAllAsync();
            var workspaceID = workspaces.Result.First().Id;
            // https://github.com/toggl/toggl_api_docs/blob/master/chapters/time_entries.md
            // ISO 8601に合わせる必要あり
            // var time = DateTime.Now.ToString("s", System.Globalization.CultureInfo.InvariantCulture) + "Z";
            var time2 = DateTime.Now.ToIsoDateStr();
            var te = new Toggl.Api.DataObjects.TimeEntry()
            {
                // IsBillable = true,
                CreatedWith = "TogglAPI.Net",
                Description = "Sent from a test code",
                ProjectId = projectId,
                Duration = 1200,
                Start = time2,
                WorkspaceId = workspaceID
            };

            try
            {
                timeEntryService.CreateAsync(te);

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

    }
}