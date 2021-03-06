﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Omex.DocumentDb.Extensions;
using Microsoft.Omex.System.Logging;
using Microsoft.Omex.System.Validation;

namespace Microsoft.Omex.DocumentDb
{
	/// <summary>
	/// Document db adapter class.
	/// </summary>
	public partial class DocumentDbAdapter : IDocumentDbAdapter
	{
		/// <summary>
		/// Lazy field containing the task that returns an IDocumentClient.
		/// </summary>
		private readonly Lazy<Task<IDocumentClient>> m_DocumentClient;


		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="clientFactory">Document db client factory.</param>
		/// <param name="config">Document db settings config.</param>
		public DocumentDbAdapter(IDocumentClientFactory clientFactory, DocumentDbSettingsConfig config = null)
		{
			Code.ExpectsArgument(clientFactory, nameof(clientFactory), TaggingUtilities.ReserveTag(0x2381b1dc /* tag_961h2 */));

			DocumentClientFactory = clientFactory;

			m_DocumentClient = new Lazy<Task<IDocumentClient>>(
				() => DocumentClientFactory.GetDocumentClientAsync(config),
				LazyThreadSafetyMode.ExecutionAndPublication);
		}


		/// <summary>
		/// Overloaded Constructor that uses the specified document db client instead of creating one.
		/// </summary>
		/// <param name="documentClient">Document db client.</param>
		public DocumentDbAdapter(IDocumentClient documentClient)
		{
			Code.ExpectsArgument(documentClient, nameof(documentClient), TaggingUtilities.ReserveTag(0x2381b1dd /* tag_961h3 */));

			m_DocumentClient = new Lazy<Task<IDocumentClient>>(
				() => Task.FromResult(documentClient),
				LazyThreadSafetyMode.ExecutionAndPublication);
		}


		/// <summary>
		/// Gets document client factory.
		/// </summary>
		/// <returns>The IDocumentClientFactory interface.</returns>
		private IDocumentClientFactory DocumentClientFactory { get; }


		/// <summary>
		/// Gets IDocumentClient. The underlying tak is lazy initialized.
		/// Once the task runs to completion, it will return the same
		/// </summary>
		/// <returns>The IDocumentClient interface.</returns>
		public Task<IDocumentClient> GetDocumentClientAsync()
		{
			return DocumentDbAdapter.ExecuteAndLogAsync(TaggingUtilities.ReserveTag(0x2381b1de /* tag_961h4 */), () => m_DocumentClient.Value);
		}


		/// <summary>
		/// Executes the async document db call and logs document db specific information to ULS.
		/// </summary>
		/// <param name="tagId">ULS tag. Should be auto generated by git tagger, no need to specify.</param>
		/// <param name="documentdbFunc">Document db delegate that returns a Resource ie. Document, Collection, Database etc.</param>
		/// <param name="caller">Caller function name. No need to specify explicitly, will be set automatically on run time.</param>
		/// <returns>The ResourceResponse of type T.</returns>
		public static Task<ResourceResponse<T>> ExecuteAndLogAsync<T>(
			uint tagId,
			Func<Task<ResourceResponse<T>>> documentdbFunc,
			[CallerMemberName] string caller = null) where T : Resource, new()
		{
			return ExecuteAndLogAsync<ResourceResponse<T>>(
				tagId,
				async () =>
				{
					ResourceResponse<T> response = await documentdbFunc().ConfigureAwait(false);
					try
					{
						LogInfo(
							caller, response.ActivityId, response.RequestCharge, (int)response.StatusCode, response.ContentLocation, tagId);
					}
					catch (Exception)
					{
					}

					return response;
				},
				caller);
		}


		/// <summary>
		/// Executes the async call and logs errors to ULS.
		/// </summary>
		/// <param name="tagId">ULS tag. Should be auto generated by git tagger, no need to specify.</param>
		/// <param name="asyncFunc">Async delegate etc.</param>
		/// <param name="caller">Caller function name. No need to specify explicitly, will be set automatically on run time.</param>
		/// <returns>The a task of type T.</returns>
		public static async Task<T> ExecuteAndLogAsync<T>(
			uint tagId,
			Func<Task<T>> asyncFunc,
			[CallerMemberName] string caller = null)
		{
			Code.ExpectsArgument(asyncFunc, nameof(asyncFunc), TaggingUtilities.ReserveTag(0x2381b1df /* tag_961h5 */));

			try
			{
				return await asyncFunc().ConfigureAwait(false);
			}
			catch (DocumentClientException exception)
			{
				ULSLogging.ReportExceptionTag(
					tagId, Categories.DocumentDb, exception, $"Operation: {caller} {exception.ToErrorMessage()}");

				throw;
			}
			catch (Exception exception)
			{
				ULSLogging.ReportExceptionTag(tagId, Categories.DocumentDb, exception,
					$"Exception detected. Caller: {caller} Error: {exception.Message}.");

				throw;
			}
		}


		/// <summary>
		/// Executes the async call and logs errors to ULS.
		/// </summary>
		/// <param name="tagId">ULS tag. Should be auto generated by git tagger, no need to specify.</param>
		/// <param name="asyncFunc">Async delegate etc.</param>
		/// <param name="caller">Caller function name. No need to specify explicitly, will be set automatically on run time.</param>
		/// <returns>The a task of type T.</returns>
		public static async Task ExecuteAndLogAsync(
			uint tagId,
			Func<Task> asyncFunc,
			[CallerMemberName] string caller = null)
		{
			Code.ExpectsArgument(asyncFunc, nameof(asyncFunc), TaggingUtilities.ReserveTag(0x2381b1e0 /* tag_961h6 */));

			try
			{
				await asyncFunc().ConfigureAwait(false);
			}
			catch (DocumentClientException exception)
			{
				ULSLogging.ReportExceptionTag(
					tagId, Categories.DocumentDb, exception, $"Operation: {caller} {exception.ToErrorMessage()}");

				throw;
			}
			catch (Exception exception)
			{
				ULSLogging.ReportExceptionTag(tagId, Categories.DocumentDb, exception,
					$"Exception detected. Caller: {caller} Error: {exception.Message}.");

				throw;
			}
		}


		/// <summary>
		/// Logs document db response info to ULS.
		/// </summary>
		/// <param name="caller">Caller function name. No need to specify explicitly, will be set automatically on run time.</param>
		/// <param name="activityId">Document db activity id.</param>
		/// <param name="charge">Charge in RU s.</param>
		/// <param name="statusCode">Status code or the response.</param>
		/// <param name="contentLocation">Location of the documentdb resource.</param>
		/// <param name="tagId">ULS tag.</param>
		private static void LogInfo(
			string caller, string activityId, double charge, int statusCode, string contentLocation, uint tagId)
		{
			ULSLogging.LogTraceTag(tagId, Categories.DocumentDb, Levels.Info,
				"Operation: {0} Cost: {1} ContentLocation: {2} StatusCode: {3} ActivityId: {4}",
				caller,
				charge,
				contentLocation,
				statusCode,
				activityId);
		}
	}
}