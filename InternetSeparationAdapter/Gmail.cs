﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Http;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace InternetSeparationAdapter
{
  public class Gmail
  {
    private const string UnreadLabel = "UNREAD";

    private static readonly ModifyMessageRequest MarkUnreadRequest =
      new ModifyMessageRequest {RemoveLabelIds = new[] {UnreadLabel}};

    private readonly CancellationToken _cancellationToken;
    private readonly GmailService _service;

    // TODO: Don't block on getting credentials -- make it an async method call for the user
    public Gmail(ClientSecrets secret, string credentialsPath, IEnumerable<string> scopes, string applicationName,
      CancellationToken cancellationToken = default(CancellationToken))
    {
      var credentials = GetCredentials(secret, scopes, credentialsPath, cancellationToken).Result;
      _cancellationToken = cancellationToken;
      if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException();
      _service = GetGmailService(credentials, applicationName);
    }

    private static Task<UserCredential> GetCredentials(ClientSecrets secret,
      IEnumerable<string> scopes,
      string credentialsPath = null,
      CancellationToken cancellation = default(CancellationToken))
    {
      var credPath = credentialsPath ?? Config.DefaultCredentialsPath;
      Console.WriteLine("Credential file will be saved to: " + credPath);
      return GoogleWebAuthorizationBroker.AuthorizeAsync(
        secret,
        scopes,
        "user",
        cancellation,
        new FileDataStore(credPath, true));
    }

    private static GmailService GetGmailService(IConfigurableHttpClientInitializer credentials, string applicationName)
    {
      return new GmailService(new BaseClientService.Initializer()
      {
        HttpClientInitializer = credentials,
        ApplicationName = applicationName,
      });
    }

    private UsersResource.MessagesResource.ListRequest MakeUnreadMessageListRequest(string label)
    {
      var request = _service.Users.Messages.List("me");
      request.LabelIds = new[] {label, UnreadLabel};
      return request;
    }

    public async Task<IList<Google.Apis.Gmail.v1.Data.Message>> GetUnreadMessage(string label)
    {
      var request = MakeUnreadMessageListRequest(label);
      var execution = await request.ExecuteAsync(_cancellationToken);
      if (_cancellationToken.IsCancellationRequested) return null;
      return execution.Messages ?? Enumerable.Empty<Google.Apis.Gmail.v1.Data.Message>().ToList();
    }

    private UsersResource.MessagesResource.GetRequest MakeGetMessageRequest(string id,
      UsersResource.MessagesResource.GetRequest.FormatEnum format =
        UsersResource.MessagesResource.GetRequest.FormatEnum.Full)
    {
      var request =  _service.Users.Messages.Get("me", id);
      request.Format = format;
      return request;
    }

    public IEnumerable<Task<Google.Apis.Gmail.v1.Data.Message>>
      FetchMessages(IEnumerable<Google.Apis.Gmail.v1.Data.Message> messages,
      UsersResource.MessagesResource.GetRequest.FormatEnum format =
        UsersResource.MessagesResource.GetRequest.FormatEnum.Full)
    {
      return messages.Select(async messageMeta =>
      {
        var messageRequest = MakeGetMessageRequest(messageMeta.Id, format);
        var message = await messageRequest.ExecuteAsync(_cancellationToken);
        return _cancellationToken.IsCancellationRequested ? null : message;
      });
    }

    public IEnumerable<Task<Google.Apis.Gmail.v1.Data.Message>> MarkRead(
      IEnumerable<Google.Apis.Gmail.v1.Data.Message> messages)
    {
      return messages.Select(message =>
      {
        var request = _service.Users.Messages.Modify(MarkUnreadRequest, "me", message.Id);
        return request.ExecuteAsync(_cancellationToken);
      });
    }
  }
}
