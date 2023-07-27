using LogicAppUnit.Helper;
using LogicAppUnit.Mocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;

namespace LogicAppUnit.Samples.LogicApps.Tests.ChunkedTransferWorkflow
{
    /// <summary>
    /// Test cases for the <i>chunked-transfer-workflow</i> workflow which uses a chunked transfer mode within HTTP action
    /// </summary>
    [TestClass]
    public class ChunkedTransferWorkflowTest : WorkflowTestBase
    {
        private const string _WebHookRequestApiKey = "serviceone-auth-webhook-apikey";

        [TestInitialize]
        public void TestInitialize()
        {
            Initialize(Constants.LOGIC_APP_TEST_EXAMPLE_BASE_PATH, Constants.CHUNKED_TRANSFER_WORKFLOW);
        }

        [ClassCleanup]
        public static void CleanResources()
        {
            Close();
        }

        [TestMethod]
        public void ChunkedTransferWorkflow_When_HTTP_Action_Returns_Should_Be_Successful()
        {
            // Override one of the settings in the local settings file
            var settingsToOverride = new Dictionary<string, string>() { { "ServiceTwo-DefaultAddressType", "physical" } };

            using (ITestRunner testRunner = CreateTestRunner(settingsToOverride))
            {
                // Configure mock responses
                testRunner
                    .AddMockResponse(
                        MockRequestMatcher.Create()
                        .UsingGet()
                        .WithPath(PathMatchType.Exact, "/api/v1/data"))
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithSuccess()
                        .WithContent(GetDataResponse));

                testRunner
                    .AddMockResponse(
                        MockRequestMatcher.Create()
                        .UsingPost()
                        .WithPath(PathMatchType.Exact, "/api/v1.1/upload"))
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithSuccess()
                        .WithContentAsPlainText("success"));

                // Run the workflow
                // var workflowResponse = testRunner.TriggerWorkflow(
                //     GetWebhookRequest(),
                //     HttpMethod.Post,
                //     new Dictionary<string, string> { { "x-api-key", _WebHookRequestApiKey } });

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                     // ContentHelper.CreateJsonStringContent(x.ToString()),
                     ContentHelper.CreateJsonStringContent(""),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                testRunner.ExceptionWrapper(() => Assert.AreEqual(HttpStatusCode.Accepted, workflowResponse.StatusCode));

                // Check action result
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("GET"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("POST"));

                // Check request to API
                var request = testRunner.MockRequests.First(r => r.RequestUri.AbsolutePath == "/api/v1/data");
                Assert.AreEqual(HttpMethod.Get, request.Method);
            }
        }

        private static StringContent GetDataResponse()
        {
            return ContentHelper.CreateJsonStringContent(new
            {
                id = 54624,
                title = "Mr",
                firstName = "Peter",
                lastName = "Smith",
                dateOfBirth = "1970-04-25",
                languageCode = "en-GB",
                address = new
                {
                    line1 = "8 High Street",
                    line2 = (string)null,
                    line3 = (string)null,
                    town = "Luton",
                    county = "Bedfordshire",
                    postcode = "LT12 6TY",
                    countryCode = "UK",
                    countryName = "United Kingdom"
                },
                extra = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Integer et nisl in tellus sodales aliquet in id sem. Suspendisse cursus mollis erat eu ullamcorper. Nulla congue id odio at facilisis. Sed ultrices dolor nisi, sit amet cursus leo pellentesque eget. Praesent sagittis ligula leo. Vestibulum varius eros posuere tortor tristique eleifend. Praesent ornare accumsan nisi sed auctor. Fusce ullamcorper nisi nec mi euismod, in efficitur quam volutpat.Vestibulum at iaculis felis. Fusce augue sem, efficitur ut vulputate quis, cursus nec mi. Nulla sagittis posuere ornare. Morbi lectus eros, luctus non condimentum eget, pretium eget sem. Aliquam convallis sed sem accumsan ultricies. Quisque commodo at odio sit amet iaculis. Curabitur nec lectus vel leo tristique aliquam et a ipsum. Duis tortor augue, gravida sed dui ac, feugiat pulvinar ex. Integer luctus urna at mauris feugiat, nec mattis elit mattis. Fusce dictum odio quis semper blandit. Pellentesque nunc augue, elementum sit amet nunc et."
            });
        }

    }
}