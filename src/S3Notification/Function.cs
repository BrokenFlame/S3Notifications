using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization;
using System.Text.Json.Nodes;
using Amazon.Lambda.S3Events;
using Amazon.Auth;
using Amazon.S3;
using SendToSlack;
using System.Text.RegularExpressions;
using static System.Net.WebRequestMethods;
using Amazon.S3.Model;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Web;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace S3Notification;

public class Function
{
    IAmazonS3 S3Client { get; set; }

    /// <summary>
    /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
    /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
    /// region the Lambda function is executed in.
    /// </summary>
    public Function()
    {
        S3Client = new AmazonS3Client();
    }

    /// <summary>
    /// Constructs an instance with a preconfigured S3 client. This can be used for testing outside of the Lambda environment.
    /// </summary>
    /// <param name="s3Client">The service client to access Amazon S3.</param>
    public Function(IAmazonS3 s3Client)
    {
        this.S3Client = s3Client;
    }

    /// <summary>
    /// This method is called for every Lambda invocation. This method takes in an S3 event object and can be used 
    /// to respond to S3 notifications.
    /// </summary>
    /// <param name="evnt">The event for the Lambda function handler to process.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    public async Task FunctionHandler(S3Event evnt, ILambdaContext context)
    {
        context.Logger.LogLine("Function Started");
        string srcBucket = Environment.GetEnvironmentVariable("srcbucket") ?? @"prod-shared-infra-sftp";
        string srcFolder = Environment.GetEnvironmentVariable("srcfolder") ?? @"ClearCourse/Merchant-Onboarding/Complete/";
        string destBucket = Environment.GetEnvironmentVariable("destbucket") ?? @"prod-shared-infra-sftp";
        string destFolder = Environment.GetEnvironmentVariable("destfolder") ?? @"Novuna/Supporting Documents/";
        string copyFile = Environment.GetEnvironmentVariable("copyfile") ?? @"true";
        string moveFileInsteadofDelete = Environment.GetEnvironmentVariable("delorgfile") ?? @"false";
        string slackfile = Environment.GetEnvironmentVariable("slackfile") ?? @"false";
        // string extentsionRegEx = Environment.GetEnvironmentVariable("extRegEx") ?? @"(\\/[a-z_\\-\\s0-9\\.]+)+\\.(xls|xlsx)$";




        bool copyFileToNewDestinationFolder = true;
        if (bool.TryParse(copyFile, out copyFileToNewDestinationFolder))
        {
            context.Logger.LogLine(string.Format("Copy environment variable [copyfile] set to {0}.", copyFileToNewDestinationFolder));
        }
        else
        {
            context.Logger.LogLine(string.Format(
                "Unable to parse the Copy file to new Destination variable [copyfile], defaulting to true."));
        }

        bool deleteAfterCopy = false;
        if (bool.TryParse(moveFileInsteadofDelete, out deleteAfterCopy))
        {
            context.Logger.LogLine(string.Format("Delete After Copy environment variable [delorgfile] set to {0}.", deleteAfterCopy));
        }
        else
        {
            context.Logger.LogLine(string.Format(
                "Unable to parse the Delete After Copy environment variable [delorgfile], defaulting to true."));
        }

        bool sendFileToSlack = false;
        if (bool.TryParse(slackfile, out sendFileToSlack))
        {
            context.Logger.LogLine(string.Format("Send File to Slack environment variable [slackfile] set to {0}.", sendFileToSlack));
        }
        else
        {
            context.Logger.LogLine(string.Format(
                "Unable to parse the Send File to Slack environment variable [slackfile], defaulting to true."));
        }

        string token = Environment.GetEnvironmentVariable("token") ?? "<Default Token here>";
        string channelName = Environment.GetEnvironmentVariable("channelName") ?? "s3-notifications";

        var eventRecords = evnt.Records ?? new List<S3Event.S3EventNotificationRecord>();
        foreach (var record in eventRecords)
        {
            var s3Event = record.S3;
            if (s3Event == null)
            {
                continue;
            }
            string srcObjectKey = HttpUtility.UrlDecode(s3Event.Object.Key);
            context.Logger.LogLine(string.Format("New file detected at s3://{0}/{1}.", s3Event.Bucket.Name, srcObjectKey));
            try
            {
                //bool matchesFileExt = Regex.Match(s3Event.Object.Key, string.Format("@{0}", extentsionRegEx)).Success;
                //if (matchesFileExt)
                //{
                //    context.Logger.LogLine(string.Format(@"File extentions match the filter Regex: {0}", extentsionRegEx));
                //}
                //else
                //{
                //    context.Logger.LogLine(string.Format(@"File extentions did not match the filter Regex: {0}", extentsionRegEx));
                //}
                if (s3Event.Bucket.Name == srcBucket && srcObjectKey.StartsWith(srcFolder))
                {
                    context.Logger.LogLine(string.Format("Criteria for action has been met, executing action."));
                    var response = await this.S3Client.GetObjectMetadataAsync(s3Event.Bucket.Name, srcObjectKey);

                    string filename = Regex.Match(srcObjectKey, @".*\/([^\/]+$)").Groups[1].Value;
                    context.Logger.LogLine(string.Format("File name detected is: {0}", filename));
                    //string fileExt = Regex.Match(s3Event.Object.Key, @".([a-z_\\-\\s0-9\\]+)$").Groups[1].Value;

                    //string destKey = destFolder + filename;
                    string destKey = srcObjectKey.Replace(srcFolder, destFolder);
                    context.Logger.LogLine(string.Format("Calculated destination is: {0}/{1}",  s3Event.Bucket.Name, destKey));

                    var newFileMsg = new SlackMessageTypes.SlackMessage
                    {
                        channel = channelName,
                        text = "New File Detected",
                        as_user = true,
                        attachments = new SlackMessageTypes.SlackAttachment[]
                        {
                            new SlackMessageTypes.SlackAttachment
                            {
                                fallback = "File detected",
                                text = string.Format("s3://{0}/{1}.", s3Event.Bucket.Name, srcObjectKey),
                                color = "good"
                            }
                        }
,
                    };

                    SlackMessageTypes.SendMessageAsync(token, newFileMsg).Wait();
                    context.Logger.LogLine("Discovery information sent to slack.");

                    if (copyFileToNewDestinationFolder)
                    {
                        context.Logger.LogLine(string.Format("Copying file from s3://{0}/{1}. to s3://{2}/{3}.", s3Event.Bucket.Name, srcObjectKey, destBucket, destKey));
                        var copyResponse = await this.S3Client.CopyObjectAsync(s3Event.Bucket.Name, srcObjectKey, destBucket, destKey);

                        var fileMovedMsg = new SlackMessageTypes.SlackMessage
                        {
                            channel = channelName,
                            text = "File Copied",
                            as_user = true,
                            attachments = new SlackMessageTypes.SlackAttachment[]
                            {
                            new SlackMessageTypes.SlackAttachment
                            {
                                fallback = "File now in pickup location",
                                text = string.Format("From s3://{0}/{1}. To s3://{2}/{3}. SFTP users may collect the file from sftp://sftp.dekopay.com/{3}", s3Event.Bucket.Name, srcObjectKey, destBucket, destKey),
                                color = "good"
                            }
                            }
                        };
                        SlackMessageTypes.SendMessageAsync(token, fileMovedMsg).Wait();
                    }

                    if (deleteAfterCopy)
                    {
                        context.Logger.LogLine(string.Format("Deleting orginal file at s3://{0}/{1}.", s3Event.Bucket.Name, srcObjectKey));
                        var deleteResponse = await this.S3Client.DeleteObjectAsync(s3Event.Bucket.Name, srcObjectKey);

                        var fileMovedMsg = new SlackMessageTypes.SlackMessage
                        {
                            channel = channelName,
                            text = "File Moved",
                            as_user = true,
                            attachments = new SlackMessageTypes.SlackAttachment[]
                            {
                            new SlackMessageTypes.SlackAttachment
                            {
                                fallback = "File now in pickup location",
                                text = string.Format("From s3://{0}/{1}. To s3://{2}/{3}. SFTP users may collect the file from sftp://sftp.dekopay.com/{3}", s3Event.Bucket.Name, srcObjectKey, destBucket, destKey),
                                color = "good"
                            }
                            }
                        };
                        SlackMessageTypes.SendMessageAsync(token, fileMovedMsg).Wait();
                    }

                    if (sendFileToSlack)
                    {
                        context.Logger.LogLine(string.Format("Reading file content at s3://{0}/{1}, to send to Slack", destBucket, destKey));
                        var s3ObjectResponse = await this.S3Client.GetObjectAsync(destBucket, destKey);
                        MemoryStream memoryStream = new MemoryStream();

                        using (Stream responseStream = s3ObjectResponse.ResponseStream)
                        {
                            responseStream.CopyTo(memoryStream);
                        }
                        var channelId = Task.Run(async () => await SlackMessageTypes.GetSlackChannelIdAsync(token, channelName)).GetAwaiter().GetResult();
                        context.Logger.LogLine(string.Format("Sending file content at s3://{0}/{1}, to Slack", destBucket, destKey));
                        await SlackMessageTypes.UploadFileUsingGetUploadURLExternal(token, filename, memoryStream.ToArray(), channelId, "New file avilable.");
                    }
                }
            }
            catch (Exception e)
            {
                context.Logger.LogError(e.Message);
                context.Logger.LogError(e.StackTrace);
                throw;
            }
            context.Logger.LogLine("Function Completed");
        }
    }
}
// dotnet lambda deploy-function ClearCourseS3Function --function-role ClearCourseS3Function-role-jqspebj4
// aws s3api put-bucket-notification-configuration --bucket=prod-shared-infra-sftp --notification-configuration="{}"
//RegEx folder and file name match: @"/\/(?=[^\/]*(?=\/(?=[^\/]*(?=\.(?![^\/]*(?=\/))))))(.*)/gm"
