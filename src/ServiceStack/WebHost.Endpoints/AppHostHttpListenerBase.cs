using System;
using System.Net;
using System.Reflection;
using ServiceStack.Common.Utils;
using ServiceStack.WebHost.Endpoints.Extensions;
using ServiceStack.WebHost.Endpoints.Support;
using ServiceStack.ServiceHost;
using ServiceStack.WebHost.Endpoints.Handlers;

namespace ServiceStack.WebHost.Endpoints
{
	/// <summary>
	/// Inherit from this class if you want to host your web services inside a 
	/// Console Application, Windows Service, etc.
	/// 
	/// Usage of HttpListener allows you to host webservices on the same port (:80) as IIS 
	/// however it requires admin user privillages.
	/// </summary>
	public abstract class AppHostHttpListenerBase 
		: HttpListenerBase
	{
		protected AppHostHttpListenerBase() {}

		protected AppHostHttpListenerBase(string serviceName, params Assembly[] assembliesWithServices)
			: base(serviceName, assembliesWithServices)
		{
			EndpointHostConfig.Instance.ServiceStackHandlerFactoryPath = null;
			EndpointHostConfig.Instance.MetadataRedirectPath = "metadata";
		}

		protected AppHostHttpListenerBase(string serviceName, string handlerPath, params Assembly[] assembliesWithServices)
			: base(serviceName, assembliesWithServices)
		{
			EndpointHostConfig.Instance.ServiceStackHandlerFactoryPath = string.IsNullOrEmpty(handlerPath)
				? null : handlerPath;			
			EndpointHostConfig.Instance.MetadataRedirectPath = handlerPath == null 
				? "metadata"
				: PathUtils.CombinePaths(handlerPath, "metadata");
		}

		protected override void ProcessRequest(HttpListenerContext context)
		{
			if (string.IsNullOrEmpty(context.Request.RawUrl)) return;

			var operationName = context.Request.GetOperationName();

			var httpReq = new HttpListenerRequestWrapper(operationName, context.Request);
			var httpRes = new HttpListenerResponseWrapper(context.Response);
			var handler = ServiceStackHttpHandlerFactory.GetHandler(httpReq);

			var serviceStackHandler = handler as IServiceStackHttpHandler;
			if (serviceStackHandler != null)
			{
				serviceStackHandler.ProcessRequest(httpReq, httpRes, operationName);
				httpRes.Close();
				return;
			}

			var serviceStackAsyncHandler = handler as IServiceStackHttpAsyncHandler;
			if (serviceStackAsyncHandler != null)
			{
                var restHandler = serviceStackAsyncHandler as AsyncRestHandler;
                if (restHandler != null)
                    httpReq.OperationName = restHandler.RestPath.RequestType.Name;

				Action<IServiceResult> callback = result =>
				{
					serviceStackAsyncHandler.EndProcessRequest(httpReq, httpRes, result);
					httpRes.Close();
				};
				serviceStackAsyncHandler.BeginProcessRequest(httpReq, httpRes, callback);
				return;
			}

			throw new NotImplementedException("Cannot execute handler: " + handler + " at PathInfo: " + httpReq.PathInfo);
		}
	}
}