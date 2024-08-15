﻿using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using CodeFlows.Workspace.Common.Configuration;
using ConductorSharp.Engine;
using ConductorSharp.Engine.Builders.Metadata;
using LibGit2Sharp;
using MediatR;
using Microsoft.Extensions.Logging;
using Octokit;
using Serilog.Core;

namespace CodeFlows.Workspace.Github.Workers
{
    public record CreatePullRequest : IRequest<CreatePullRequest.Response>
    {
        [Required]
        public required string RepositoryPath { get; set; }

        [Required]
        public required string RepositoryOwner { get; set; }

        [Required]
        public required string RepositoryName { get; set; }

        [Required]
        public required string OriginalOwner { get; set; }

        [Required]
        public required string PullRequestTitle { get; set; }

        [Required]
        public required string PullRequestDescription { get; set; }

        [Required]
        public required string BranchName { get; set; }

        [Required]
        public required string BaseRef { get; set; }

        public record Response { }

        [OriginalName("gh_create_pr")]
        public class Handler(
            UsernamePasswordCredentials gitCredentials,
            GitHubClient githubClient,
            ILogger<CreatePullRequest.Handler> logger
        ) : TaskRequestHandler<CreatePullRequest, Response>
        {
            private readonly UsernamePasswordCredentials gitCredentials = gitCredentials;
            private readonly GitHubClient githubClient = githubClient;
            private readonly ILogger<Handler> logger = logger;

            public override async Task<Response> Handle(
                CreatePullRequest request,
                CancellationToken cancellationToken
            )
            {
                var directoryInfo = new DirectoryInfo(
                    Path.Join(StorageConfiguration.RootDirectoryPath, request.RepositoryPath)
                );

                if (!Directory.Exists(Path.Join(directoryInfo.FullName, ".git")))
                {
                    throw new InvalidOperationException("Repository with .git directory not found");
                }

                using var repo = new LibGit2Sharp.Repository(directoryInfo.FullName);

                var remote = repo.Network.Remotes["origin"];
                var options = new PushOptions
                {
                    CredentialsProvider = (url, usernameFromUrl, types) => gitCredentials
                };

                repo.Network.Push(remote, $"refs/heads/{request.BranchName}", options);

                var pullRequest = new NewPullRequest(
                    request.PullRequestTitle,
                    $"{gitCredentials.Username}:{request.BranchName}",
                    request.BaseRef
                )
                {
                    Body = request.PullRequestDescription
                };

                var pr = await githubClient.PullRequest.Create(
                    request.OriginalOwner,
                    request.RepositoryName,
                    pullRequest
                );

                logger.LogDebug("Created pull request at {pullRequestUrl}", pr.HtmlUrl);
                return new();
            }
        }
    }
}
