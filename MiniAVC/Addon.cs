// Copyright (C) 2014 CYBUTEK
//
// This program is free software: you can redistribute it and/or modify it under the terms of the GNU
// General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without
// even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// General Public License for more details.
//
// You should have received a copy of the GNU General Public License along with this program. If not,
// see <http://www.gnu.org/licenses/>.

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

namespace MiniAVC
{
    public class Addon
    {
        private readonly AddonSettings settings;

        public Addon(string path, AddonSettings settings)
        {
            this.settings = settings;
            RunProcessLocalInfo(path);
        }

        public string Base64String
        {
            get
            {
                return LocalInfo.Base64String + RemoteInfo.Base64String;
            }
        }

        public bool HasError { get; private set; }

        public bool IsCompatible
        {
            get { return IsLocalReady && LocalInfo.IsCompatible; }
        }

        public bool IsIgnored
        {
            get
            {
                return settings.IgnoredUpdates.Contains(Base64String);
            }
        }

        public bool IsLocalReady { get; private set; }

        public bool IsProcessingComplete { get; private set; }

        public bool IsRemoteReady { get; private set; }

        public bool IsUpdateAvailable
        {
            get
            {
                bool b = this.IsProcessingComplete &&
                  this.LocalInfo.Version != null &&
                  this.RemoteInfo.Version != null &&
                  this.RemoteInfo.Version > this.LocalInfo.Version &&
                  // this.RemoteInfo.IsCompatibleKspVersion && 
                  this.RemoteInfo.IsCompatible &&
                  this.RemoteInfo.IsCompatibleGitHubVersion;

                return b;
            }
        }
        public AddonInfo LocalInfo { get; private set; }

        public string Name
        {
            get { return LocalInfo.Name; }
        }

        public AddonInfo RemoteInfo { get; private set; }

        public AddonSettings Settings
        {
            get { return settings; }
        }

        public void RunProcessLocalInfo(string file)
        {
            ThreadPool.QueueUserWorkItem(ProcessLocalInfo, file);
        }

        public void RunProcessRemoteInfo()
        {
            ThreadPool.QueueUserWorkItem(ProcessRemoteInfo);
        }

        private void FetchLocalInfo(string path)
        {
            using (var stream = new StreamReader(File.OpenRead(path)))
            {
                LocalInfo = new AddonInfo(path, stream.ReadToEnd());
                IsLocalReady = true;

                if (LocalInfo.ParseError)
                {
                    SetHasError();
                }
            }
        }

		private void FetchRemoteInfo()
		{
			//using (var www = new WWW(Uri.EscapeUriString(LocalInfo.Url)))
			//{
			//    while (!www.isDone)
			//    {
			//        Thread.Sleep(100);
			//    }
			//    if (www.error == null)
			//    {
			try
			{
				SetRemoteInfo(HttpWebRequestBeginGetRequest.GetRequest(LocalInfo.Url));
			}
			catch (Exception ex)
			{
				Logger.Log("EXCEPTION: " + ex);
				SetLocalInfoOnly();
			}
			//}
			//else
			//{

			//}
			//}
		}

        private void ProcessLocalInfo(object state)
        {
            try
            {
                var path = (string)state;
                if (File.Exists(path))
                {
                    FetchLocalInfo(path);
                    RunProcessRemoteInfo();
                }
                else
                {
                    Logger.Log("File Not Found: " + path);
                    SetHasError();
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
                SetHasError();
            }
        }

        private void ProcessRemoteInfo(object state)
        {
            try
            {
                if (settings.FirstRun)
                {
                    return;
                }

                if (!settings.AllowCheck || string.IsNullOrEmpty(LocalInfo.Url))
                {
                    SetLocalInfoOnly();
                    return;
                }

                FetchRemoteInfo();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
                SetLocalInfoOnly();
            }
        }

        private void SetHasError()
        {
            HasError = true;
            IsProcessingComplete = true;
        }

        private void SetLocalInfoOnly()
        {
            RemoteInfo = LocalInfo;
            IsRemoteReady = true;
            IsProcessingComplete = true;
            Logger.Log(LocalInfo);
            Logger.Blank();
        }

        private void SetRemoteInfo(string text)
        {
            RemoteInfo = new AddonInfo(LocalInfo.Url, text);
            RemoteInfo.FetchRemoteData();

            Logger.Log("LocalInfo.Url: " + LocalInfo.Url + ",   www.text: " + text);
#if true
            if (LocalInfo.Version == RemoteInfo.Version)
            {
                Logger.Log("Identical remote version found: Using remote version information only.");
                Logger.Log(RemoteInfo);
                Logger.Blank();
                LocalInfo = RemoteInfo;
            }
            else
#endif
            {
                Logger.Log(LocalInfo);
                Logger.Log(RemoteInfo + "\n\tUpdateAvailable: " + IsUpdateAvailable);
                Logger.Blank();
            }

            IsRemoteReady = true;
            IsProcessingComplete = true;
        }
    }

	class HttpWebRequestBeginGetRequest
	{
		private static ManualResetEvent allDone = new ManualResetEvent(false);
		private static string webResponse = String.Empty;

		public static string GetRequest(string url) { 
			// Create a new HttpWebRequest object.
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

			request.ContentType = "application/x-www-form-urlencoded";

			// Set the Method property to 'POST' to post data to the URI.
			// request.Method = "POST";
			request.Method = "GET";

			// start the asynchronous operation
			request.BeginGetRequestStream(new AsyncCallback(GetRequestStreamCallback), request);

			// Keep the main thread from continuing while the asynchronous
			// operation completes. A real world application
			// could do something useful such as updating its user interface. 
			allDone.WaitOne();

			return webResponse;
		}

		private static void GetRequestStreamCallback(IAsyncResult asynchronousResult)
		{
			HttpWebRequest request = (HttpWebRequest)asynchronousResult.AsyncState;

			// End the operation
			Stream postStream = request.EndGetRequestStream(asynchronousResult);

			//Console.WriteLine("Please enter the input data to be posted:");
			string postData = String.Empty;

			if (!String.IsNullOrEmpty(postData))
			{
				// Convert the string into a byte array.
				byte[] byteArray = Encoding.UTF8.GetBytes(postData);

				// Write to the request stream.
				postStream.Write(byteArray, 0, postData.Length);
			}
			postStream.Close();

			// Start the asynchronous operation to get the response
			request.BeginGetResponse(new AsyncCallback(GetResponseCallback), request);
		}

		private static void GetResponseCallback(IAsyncResult asynchronousResult)
		{
			HttpWebRequest request = (HttpWebRequest)asynchronousResult.AsyncState;

			// End the operation
			HttpWebResponse response = (HttpWebResponse)request.EndGetResponse(asynchronousResult);
			Stream streamResponse = response.GetResponseStream();
			StreamReader streamRead = new StreamReader(streamResponse);
			webResponse = streamRead.ReadToEnd();
			//Console.WriteLine(webResponse);
			// Close the stream object
			streamResponse.Close();
			streamRead.Close();

			// Release the HttpWebResponse
			response.Close();
			allDone.Set();
		}
	}
}
