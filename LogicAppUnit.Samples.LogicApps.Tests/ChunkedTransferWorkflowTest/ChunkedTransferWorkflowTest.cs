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
using System.IO;

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
            /* the chunking mock below doesn't seem to work on macos (intel)
            sample error -> {"code":"InvalidProtocolResponse","message":"The response to partial content upload request is not valid. The location header value returned in the response 'http://localhost:7075/api/v1.1/287124d3-a604-4b35-97fa-2cc2d7332772' must be a well formed absolute URI not referencing local host or UNC path."}'
            [2023-08-03T05:02:42.689Z] Workflow action ends. flowName='chunked-transfer-workflow', actionName='POST', flowId='e9db21e1366f49988ac2242b3bb0b2bf', flowSequenceId='08585105679261553867', flowRunSequenceId='08585105679238406407347027054CU00', correlationId='83e6e8a6-0163-4033-83ef-76d076bbbb90', status='Failed', statusCode='InvalidProtocolResponse', error='{"code":"InvalidProtocolResponse","message":"The response to partial content upload request is not valid. The location header value returned in the response 'http://localhost:7075/api/v1.1/287124d3-a604-4b35-97fa-2cc2d7332772' must be a well formed absolute URI not referencing local host or UNC path."}', durationInMilliseconds='113', inputsContentSize='1399', outputsContentSize='-1', extensionVersion='1.28.4.0', siteName='UNDEFINED_SITE_NAME', slotName='', actionTrackingId='79df69be-b78a-4444-b2ab-a54acab3b112', clientTrackingId='08585105679238406407347027054CU00', properties='{"$schema":"2016-06-01","startTime":"2023-08-03T05:02:42.576536Z","endTime":"2023-08-03T05:02:42.689742Z","status":"Failed","code":"InvalidProtocolResponse","executionClusterType":"Classic","resource":{"workflowId":"e9db21e1366f49988ac2242b3bb0b2bf","workflowName":"chunked-transfer-workflow","runId":"08585105679238406407347027054CU00","actionName":"POST"},"correlation":{"actionTrackingId":"79df69be-b78a-4444-b2ab-a54acab3b112","clientTrackingId":"08585105679238406407347027054CU00"},"error":{"code":"InvalidProtocolResponse","message":"The response to partial content upload request is not valid. The location header value returned in the response 'http://localhost:7075/api/v1.1/287124d3-a604-4b35-97fa-2cc2d7332772' must be a well formed absolute URI not referencing local host or UNC path."},"api":{},"isV2Threshold":false}', actionType='Http', sequencerType='Linear', flowScaleUnit='cu00', platformOptions='RunDistributionAcrossPartitions, RepetitionsDistributionAcrossSequencers, RunIdTruncationForJobSequencerIdDisabled, RepetitionPreaggregationEnabled', retryHistory='', failureCause='', overrideUsageConfigurationName='', hostName='localhost', activityId='5af6c639-fd29-476d-901a-85d903c02934'.
            */

            // mock chunk as per https://github.com/LogicAppUnit/TestingFramework/issues/24#issuecomment-1655474667
            int chunkSizeInBytes = 512;
            string chunkEndpoint = Guid.NewGuid().ToString();
            Stream requestStream = new MemoryStream();
            
            // Override one of the settings in the local settings file
            var settingsToOverride = new Dictionary<string, string>() { { "ServiceTwo-DefaultAddressType", "physical" } };

            using (ITestRunner testRunner = CreateTestRunner(settingsToOverride))
            {
                // mock chunking (some of it with fluent api)
                testRunner
                    .AddMockResponse(
                        MockRequestMatcher.Create()
                        .UsingPost()
                        .WithPath(PathMatchType.Exact, "/api/v1.1/upload")
                        .WithHeader("x-ms-transfer-mode", "chunked"))
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithSuccess()
                        .WithHeader("Location", $"{MockTestWorkflowHostUri}/api/v1.1/{chunkEndpoint}")
                        .WithHeader("x-ms-chunk-size", chunkSizeInBytes.ToString())
                    );
                    
                // mock the rest of chunking with delegate api
                testRunner.AddApiMocks = (request) => {
                    HttpResponseMessage mockedResponse = new HttpResponseMessage();
                    mockedResponse.RequestMessage = request;

                    if (request.RequestUri.AbsolutePath == $"/api/v1.1/{chunkEndpoint}" && request.Method == HttpMethod.Patch) {
                        // append content
                        var content = request.Content.ReadAsByteArrayAsync().Result;
                        requestStream.Write(content, 0, content.Length);
                        var contentRange = request.Content.Headers.ContentRange;
                        mockedResponse.StatusCode = HttpStatusCode.OK;
                        mockedResponse.Headers.Add("Range", $"{contentRange.Unit}=0-{contentRange.To}");
                    }
                    return mockedResponse;
                };
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