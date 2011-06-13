﻿using System;
using System.IO;
using System.Web;
using ExtDirectHandler.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExtDirectHandler
{
	public class DirectHttpHandler : IHttpHandler
	{
		private static Metadata _metadata;
		private static ObjectFactory _objectFactory = new ObjectFactory();

		public bool IsReusable
		{
			get { return false; }
		}

		internal static void SetActionMetadatas(Metadata metadata)
		{
			if(_metadata != null)
			{
				throw new Exception("Already configured");
			}
			_metadata = metadata;
		}

		public static void SetObjectFactory(ObjectFactory factory)
		{
			if(_objectFactory != null)
			{
				throw new Exception("Already configured");
			}
			_objectFactory = factory;
		}

		public void ProcessRequest(HttpContext context)
		{
			switch(context.Request.HttpMethod)
			{
				case "GET":
					DoGet(context.Request, context.Response);
					break;
				case "POST":
					DoPost(context.Request, context.Response);
					break;
			}
		}

		private void DoPost(HttpRequest httpRequest, HttpResponse httpResponse)
		{
			DirectRequest[] requests;
			if(httpRequest.ContentType.Contains("application/x-www-form-urlencoded"))
			{
				requests = new[]{ new DirectRequest() };
				foreach(string key in httpRequest.Params.AllKeys)
				{
					switch(key)
					{
						case "extTID":
							requests[0].Tid = int.Parse(httpRequest.Params[key]);
							break;
						case "extAction":
							requests[0].Action = httpRequest.Params[key];
							break;
						case "extMethod":
							requests[0].Method = httpRequest.Params[key];
							break;
						case "extType":
							requests[0].Type = httpRequest.Params[key];
							break;
						case "extUpload":
							requests[0].Upload = bool.Parse(httpRequest.Params[key]);
							break;
						default:
							requests[0].Data = (requests[0].Data ?? new JToken[] { new JObject() });
							((JObject)requests[0].Data[0]).Add(key, new JValue(httpRequest.Params[key]));
							break;
					}
				}
			}
			else
			{
				JToken jToken = JToken.Load(new JsonTextReader(new StreamReader(httpRequest.InputStream, httpRequest.ContentEncoding)));
				requests = new JsonSerializer().Deserialize<DirectRequest[]>(new JTokenReader(jToken.Type == JTokenType.Array ? jToken : new JArray(jToken)));
			}
			var responses = new DirectResponse[requests.Length];
			for(int i = 0; i < requests.Length; i++)
			{
				responses[i] = new DirectHandler(_objectFactory, _metadata).Handle(requests[i]);
			}
			using(var jsonWriter = new JsonTextWriter(new StreamWriter(httpResponse.OutputStream, httpResponse.ContentEncoding)))
			{
				new JsonSerializer().Serialize(jsonWriter, responses.Length == 1 ? (object)responses[0] : responses);
			}
		}

		private void DoGet(HttpRequest request, HttpResponse response)
		{
			string ns = request.QueryString["ns"];
			response.ContentType = "text/javascript";
			string url = request.Url.GetComponents(UriComponents.Scheme | UriComponents.Host | UriComponents.Port | UriComponents.Path, UriFormat.Unescaped);
			response.Write(new DirectApiBuilder(_metadata).BuildApi(ns, url));
		}
	}
}