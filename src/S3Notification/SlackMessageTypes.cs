using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Web;
using System.IO;
using System.Diagnostics.Eventing.Reader;
using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices.ComTypes;
using System.Net;
using System.Runtime.CompilerServices;
using System.Collections.Specialized;
using static System.Net.WebRequestMethods;

namespace SendToSlack
{
    internal class SlackMessageTypes
    {
        private static readonly HttpClient client = new HttpClient();

        // reponse from message methods
        public class SlackMessageResponse
        {
            public bool ok { get; set; }
            public string error { get; set; }
            public string channel { get; set; }
            public string ts { get; set; }
        }

        // a slack message
        public class SlackMessage
        {
            public string channel { get; set; }
            public string text { get; set; }
            public bool as_user { get; set; }
            public SlackAttachment[] attachments { get; set; }
        }

        // a slack message attachment
        public class SlackAttachment
        {
            public string fallback { get; set; }
            public string pretext { get; set; }
            public string text { get; set; }
            public string thumb_url { get; set; }
            public string image_url { get; set; }
            public string color { get; set; }
            public string footer { get; set; }
            public string footer_icon { get; set; }
            public string author_name { get; set; }
            public string author_link { get; set; }
            public string author_icon { get; set; }

        }

        // reponse from file methods

        public class SlackGetUploadURLExternal
        {
            public String filename { get; set; }
            public ulong length { get; set; }
        }
        public class SlackGetUploadURLExternalResponse
        {
            public bool ok { get; set; }
            public String error { get; set; }
            public String upload_url { get; set; }
            public String file_id { get; set; }
        }

        public class SlackCompleteUploadURLExternal
        {
            public SlackFile[] files { get; set; }
            public String channel_id { get; set; }
            public String initial_comment { get; set; }
        }

        public class SlackCompleteUploadURLExternalResponse
        {
            public bool ok { get; set; }
            public String error { get; set; }
            public SlackFile[] files { get; set; }
        }

        // a slack file
        public class SlackFile
        {
            public String id { get; set; }
            public String title { get; set; }
        }

        public class SlackConversationsListResponse
        {
            public bool ok { get; set; }
            public string error { get; set; }
            public SlackChannelInfo[] channels { get; set; }
            public SlackConversationsListResponseMetadata response_metadata { get; set; }
        }

        public class SlackConversationsListResponseMetadata
        {
            public string next_cursor { get; set; }
        }

        public class SlackChannelInfo
        {
            public string id { get; set; }
            public string name { get; set; }
            public bool is_channel { get; set; }
            public bool is_group { get; set; }
            public bool is_im { get; set; }
            public ulong created { get; set; }
            public string creator { get; set; }
            public bool is_archived { get; set; }
            public bool is_general { get; set; }
            public ulong unlinked { get; set; }
            public string name_normalized { get; set; }
            public bool is_shared { get; set; }
            public bool is_ext_shared { get; set; }
            public bool is_org_shared { get; set; }
            string[] pending_shared { get; set; }
            public bool is_pending_ext_shared { get; set; }
            public bool is_member { get; set; }
            public bool is_private { get; set; }
            public bool is_mpim { get; set; }
            public ulong updated { get; set; }
            public SlackChannelTopic topic { get; set; }
            public SlackChannelPurpose purpose { get; set; }
            public String[] previous_names { get; set; }
        }

        public class SlackChannelTopic
        {
            public String value { get; set; }
            public String creator { set; get; }
            public int last_set { get; set; }
        }

        public class SlackChannelPurpose
        {
            public String value { get; set; }
            public String creator { set; get; }
            public int last_set { set; get; }
        }

        public class SlackChannel
        {
            public string channel { get; set; }
            public bool include_locale { get; set; }
            public bool include_num_members { get; set; }
        }

        public class FilesSlackSharedPublicURL
        {
            public string channel { set; get; }
            public string file { set; get; }
            public string initial_comment { set; get; }

        }





        // sends a slack message asynchronous
        // throws exception if message can not be sent
        public static async Task SendMessageAsync(string token, SlackMessage msg)
        {
            // serialize method parameters to JSON
            var content = JsonConvert.SerializeObject(msg);
            var httpContent = new StringContent(
                content,
                Encoding.UTF8,
                "application/json"
            );

            // set token in authorization header
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // send message to API
            var response = await client.PostAsync("https://slack.com/api/chat.postMessage", httpContent);

            // fetch response from API
            var responseJson = await response.Content.ReadAsStringAsync();

            // convert JSON response to object
            SlackMessageResponse messageResponse =
                JsonConvert.DeserializeObject<SlackMessageResponse>(responseJson);

            // throw exception if sending failed
            if (messageResponse.ok == false)
            {
                throw new Exception(
                    "failed to send message. error: " + messageResponse.error
                );
            }
        }

        public static async Task UploadFileUsingGetUploadURLExternal(string token, FileInfo file, string channel_id, string initialComment)
        {
            //var msg = new SlackMessageTypes.SlackGetUploadURLExternal
            //{
            //    filename = file.Name.ToString(),
            //    length = (ulong)file.Length
            //};

            //var content = JsonConvert.SerializeObject(msg);
            //var httpContent = new StringContent(
            //    content, 
            //    Encoding.UTF8, 
            //    "application/x-www-form-urlencoded"
            //    );

            string filename = file.Name.ToString();
            ulong length = (ulong)file.Length;

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync(String.Format("https://slack.com/api/files.getUploadURLExternal?filename={0}&length={1}", filename, length.ToString()));

            var responseJson = await response.Content.ReadAsStringAsync();
            SlackGetUploadURLExternalResponse messageResponse =
               JsonConvert.DeserializeObject<SlackGetUploadURLExternalResponse>(responseJson);

            if (messageResponse.ok == false)
            {
                throw new Exception(
                    "failed to send file. error:" + messageResponse.error);
            }
            else
            {
                await SendSlackFile(token, messageResponse.upload_url, file);
                await CompleteSendSlackFile(token, file, messageResponse.file_id, channel_id, initialComment);
            }
        }

        public static async Task UploadFileUsingGetUploadURLExternal(string token, string filename, byte[] bytes, string channel_id, string initialComment)
        {
            //var msg = new SlackMessageTypes.SlackGetUploadURLExternal
            //{
            //    filename = file.Name.ToString(),
            //    length = (ulong)file.Length
            //};

            //var content = JsonConvert.SerializeObject(msg);
            //var httpContent = new StringContent(
            //    content, 
            //    Encoding.UTF8, 
            //    "application/x-www-form-urlencoded"
            //    );

            var length = bytes.Length;

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync(String.Format("https://slack.com/api/files.getUploadURLExternal?filename={0}&length={1}", filename, length.ToString()));

            var responseJson = await response.Content.ReadAsStringAsync();
            SlackGetUploadURLExternalResponse messageResponse =
               JsonConvert.DeserializeObject<SlackGetUploadURLExternalResponse>(responseJson);

            if (messageResponse.ok == false)
            {
                throw new Exception(
                    "failed to send file. error:" + messageResponse.error);
            }
            else
            {
                await SendSlackFile(token, messageResponse.upload_url, bytes);
                await CompleteSendSlackFile(token, filename, messageResponse.file_id, channel_id, initialComment);
            }
        }

        public static async Task SendSlackFile(string token, string uploadUrl, FileInfo file)
        {
            using (var fileStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                //var filecontent = new StreamContent(fileStream);
                //filecontent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                //var uploadfilecontent = new MultipartFormDataContent();
                //uploadfilecontent.Add(filecontent);

                //var uploadResponse = await client.PutAsync(uploadUrl, uploadfilecontent);
                //var uploadResponsecontent = await uploadResponse.Content.ReadAsStringAsync();

                //if (!uploadResponse.IsSuccessStatusCode)
                //{
                //    throw new Exception(string.Format("File [{0}], failed to upload to {1}", file.FullName, uploadUrl));
                //}

                using (WebClient client = new WebClient())
                {
                    Uri fileUploadUri = new Uri(uploadUrl);
                    var response = await client.UploadFileTaskAsync(fileUploadUri, file.FullName);
                    var responseUTF8 = System.Text.Encoding.UTF8.GetString(response);
                    if (!responseUTF8.Contains("OK"))
                    {
                        throw new Exception(
                            String.Format(
                                "Faile to upload file to {0}. Server returned message [{1}]",
                                uploadUrl,
                                responseUTF8)
                            );
                    }
                }
            }
        }

        public static async Task SendSlackFile(string token, string uploadUrl, byte[] fileData)
        {


            using (WebClient client = new WebClient())
            {
                Uri fileUploadUri = new Uri(uploadUrl);
                var response = await client.UploadDataTaskAsync(fileUploadUri, fileData);
                var responseUTF8 = System.Text.Encoding.UTF8.GetString(response);
                if (!responseUTF8.Contains("OK"))
                {
                    throw new Exception(
                        String.Format(
                            "Faile to upload file to {0}. Server returned message [{1}]",
                            uploadUrl,
                            responseUTF8)
                        );
                }
            }

        }

        public static async Task CompleteSendSlackFile(string token, FileInfo file, string file_id, string channel_id, string initialComment)
        {
            var msg = new SlackMessageTypes.SlackCompleteUploadURLExternal
            {
                initial_comment = file.Name,
                channel_id = channel_id,
                files = new SlackFile[]
                {
                    new SlackFile
                    {
                        id = file_id,
                        title = file.Name
                    }
                }
            };
            // serialize method parameters to JSON
            var content = JsonConvert.SerializeObject(msg);
            var httpContent = new StringContent(
                content,
                Encoding.UTF8,
                "application/json"
            );

            // set token in authorization header
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // send message to API
            var response = await client.PostAsync("https://slack.com/api/files.completeUploadExternal", httpContent);

            // fetch response from API
            var responseJson = await response.Content.ReadAsStringAsync();

            // convert JSON response to object
            SlackCompleteUploadURLExternalResponse messageResponse =
                JsonConvert.DeserializeObject<SlackCompleteUploadURLExternalResponse>(responseJson);

            // throw exception if sending failed
            if (messageResponse.ok == false)
            {
                throw new Exception(
                    "failed to send file upload completion message. error: " + messageResponse.error
                );
            }
        }

        public static async Task CompleteSendSlackFile(string token, string filename, string file_id, string channel_id, string initialComment)
        {
            var msg = new SlackMessageTypes.SlackCompleteUploadURLExternal
            {
                initial_comment = filename,
                channel_id = channel_id,
                files = new SlackFile[]
                {
                    new SlackFile
                    {
                        id = file_id,
                        title = filename
                    }
                }
            };
            // serialize method parameters to JSON
            var content = JsonConvert.SerializeObject(msg);
            var httpContent = new StringContent(
                content,
                Encoding.UTF8,
                "application/json"
            );

            // set token in authorization header
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // send message to API
            var response = await client.PostAsync("https://slack.com/api/files.completeUploadExternal", httpContent);

            // fetch response from API
            var responseJson = await response.Content.ReadAsStringAsync();

            // convert JSON response to object
            SlackCompleteUploadURLExternalResponse messageResponse =
                JsonConvert.DeserializeObject<SlackCompleteUploadURLExternalResponse>(responseJson);

            // throw exception if sending failed
            if (messageResponse.ok == false)
            {
                throw new Exception(
                    "failed to send file upload completion message. error: " + messageResponse.error
                );
            }
        }



        /// <summary>
        /// Do not use unless you want everyone to see the file
        /// </summary>
        /// <param name="token"></param>
        /// <param name="channelId"></param>
        /// <param name="fileId"></param>
        /// <param name="initialComment"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task SlackFilesSharePublicURL(string token, string channelId, string fileId, string initialComment)
        {

            var msg = new SlackMessageTypes.FilesSlackSharedPublicURL
            {
                initial_comment = initialComment,
                channel = channelId,
                file = fileId
            };
            // serialize method parameters to JSON
            var content = JsonConvert.SerializeObject(msg);

            var httpContent = new StringContent(
                content,
                Encoding.UTF8,
                "application/json"
            );
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.PostAsync("https://slack.com/api/files.sharedPublicURL", httpContent);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(string.Format("Could not  make file [{0}] public", fileId));
            }
        }

        /// <summary>
        /// Get Channel ID by Channel Name
        /// </summary>
        /// <param name="token"></param>
        /// <param name="channel_name"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task<String> GetSlackChannelIdAsync(string token, string channel_name)
        {
            // set token in authorization header
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // send message to API
            var response = await client.GetAsync("https://slack.com/api/conversations.list?limit=10&types=private_channel&exclude_archived=true");
            // fetch response from API
            var responseJson = await response.Content.ReadAsStringAsync();

            // Begin Cursor movement
            // Corur as not been implemented due to time constraints. 


            SlackConversationsListResponse channelInfo = JsonConvert.DeserializeObject<SlackConversationsListResponse>(responseJson);
            if (channelInfo.ok == false)
            {
                throw new Exception("failed to send message. error: " + channelInfo.error);
            }

            var selectedChannels = from channel in channelInfo.channels
                                   where channel.name == channel_name
                                   select channel.id;

            string channel_id;
            if (selectedChannels == null)
            {
                throw new Exception(string.Format("Could not determine the channel ID for the channel {0}", channel_name));
            }
            channel_id = selectedChannels.FirstOrDefault().ToString();
            return channel_id.ToString();
        }
    }
}
