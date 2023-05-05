using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Services.Common;
using FileDiff = Microsoft.TeamFoundation.SourceControl.WebApi.FileDiff;
using GitChange = Microsoft.TeamFoundation.SourceControl.WebApi.GitChange;
using GitItem = Microsoft.TeamFoundation.SourceControl.WebApi.GitItem;
using VersionControlChangeType = Microsoft.TeamFoundation.SourceControl.WebApi.VersionControlChangeType;

namespace AZDevops_POC
{
    public class Branches
    {
        private readonly string _projectName;
        private readonly VssConnection _connection;
        private readonly GitHttpClient _gitClient;
        private readonly TeamProjectReference _project;
        private readonly GitRepository _repo;

        public Branches()
        {
            _projectName = "WebApplication1";
            _connection = new VssConnection(new Uri("[[AzDevOps-uri]]"), new VssBasicCredential("", "[[personalAccessToken]]"));
            _gitClient = _connection.GetClient<GitHttpClient>();
            var projectClient = _connection.GetClient<ProjectHttpClient>();
            _project = projectClient.GetProjects(null, top: 1).Result.FirstOrDefault();
            _repo = _gitClient.GetRepositoriesAsync(_projectName).Result.FirstOrDefault();
        }

        public IEnumerable<GitRef> ListBranches()
        {
            List<GitRef> refs = _gitClient.GetRefsAsync(_repo.Id, filter: "heads/").Result;

            Console.WriteLine("project {0}, repo {1}", _project.Name, _repo.Name);
            foreach (GitRef gitRef in refs)
            {
                Console.WriteLine("{0} {1} {2}", gitRef.Name, gitRef.ObjectId, gitRef.Url);
            }

            return refs;
        }

        public GitRefUpdateResult CreateBranch(string name)
        {
            Console.Write("Create a branch(ref)");
            
            GitRef sourceRef = _gitClient.GetRefsAsync(_repo.Id, filter: _repo.DefaultBranch.Replace("refs/", "")).Result.First();

            // create a new branch from the source
            GitRefUpdateResult refCreateResult = _gitClient.UpdateRefsAsync(
                new GitRefUpdate[] { new GitRefUpdate() {
                    OldObjectId = new string('0', 40),
                    NewObjectId = sourceRef.ObjectId,
                    Name = $"refs/heads/{name}",
                } },
                repositoryId: _repo.Id).Result.First();

            Console.WriteLine("project {0}, repo {1}, source branch {2}", _project.Name, _repo.Name, sourceRef.Name);
            Console.WriteLine("new branch {0} (success={1} status={2})", refCreateResult.Name, refCreateResult.Success, refCreateResult.UpdateStatus);
            return refCreateResult;
        }

        public GitPush CreateBranchAndPushChanges(string branchName)
        {
            Console.WriteLine($"Create a commit and push a sample file to new branch {branchName}!");
            
            GitRef sourceRef = _gitClient.GetRefsAsync(_repo.Id, filter: _repo.DefaultBranch.Replace("refs/", "")).Result.First();

            var branchFullName = $"{branchName}";
            // next, craft the branch and commit that we'll push
            GitRefUpdate newBranch = new GitRefUpdate()
            {
                Name = $"refs/heads/{branchFullName}",
                OldObjectId = sourceRef.ObjectId,
            };

            string newFileName = "xyz.md";
            GitCommitRef newCommit = new GitCommitRef()
            {
                Comment = "test123",
                Changes = new GitChange[]
                {
                    new GitChange()
                    {
                        ChangeType = VersionControlChangeType.Edit,
                        Item = new GitItem() { Path = $"/{newFileName}" },
                        NewContent = new ItemContent()
                        {
                            Content = "test123, tatsächliche änderung",
                            ContentType = ItemContentType.RawText,
                        },
                    },
                    new GitChange()
                    {
                        ChangeType = VersionControlChangeType.Edit,
                        Item = new GitItem() { Path = "/test2.txt" },
                        NewContent = new ItemContent()
                        {
                            Content = @"OriginalText

                                Zeile 2
                                Zeile 3
                                Zeile 4",
                            ContentType = ItemContentType.RawText,
                        }
                    },
                    new GitChange()
                    {
                        ChangeType = VersionControlChangeType.Add,
                        Item = new GitItem() { Path = "/Folder1/test3.txt" },
                        NewContent = new ItemContent()
                        {
                            Content = @"Text von Test3
2. Zeile
3. Zeile",
                            ContentType = ItemContentType.RawText,
                        }
                    },
                },
            };

            // create the push with the new branch and commit
            GitPush push = _gitClient.CreatePushAsync(new GitPush()
            {
                RefUpdates = new GitRefUpdate[] { newBranch },
                Commits = new GitCommitRef[] { newCommit },
            }, _repo.Id).Result;

            Console.WriteLine("project {0}, repo {1}", _project.Name, _repo.Name);
            Console.WriteLine("push {0} updated {1} to {2}",
                push.PushId, push.RefUpdates.First().Name, push.Commits.First().CommitId);

            // now clean up after ourselves (and in case logging is on, don't log these calls)
            //ClientSampleHttpLogger.SetSuppressOutput(this.Context, true);

            //// delete the branch
            //GitRefUpdateResult refDeleteResult = gitClient.UpdateRefsAsync(
            //    new GitRefUpdate[]
            //    {
            //        new GitRefUpdate()
            //        {
            //            OldObjectId = push.RefUpdates.First().NewObjectId,
            //            NewObjectId = new string('0', 40),
            //            Name = push.RefUpdates.First().Name,
            //        }
            //    },
            //    repositoryId: repo.Id).Result.First();

            // pushes and commits are immutable, so no way to clean them up
            // but the commit will be unreachable after this

            return push;
        }

        public GitPullRequest CreatePullRequest(string branchName)
        {
            
            //GitRef sourceRef = gitClient.GetRefsAsync(repo.Id, filter: repo.DefaultBranch.Replace("refs/", "")).Result.First();
            //var currentUserId = connection.AuthorizedIdentity.Id;

            TeamHttpClient teamClient = _connection.GetClient<TeamHttpClient>();
            var team = teamClient.GetTeamsAsync(_projectName, top: 1).Result.FirstOrDefault();
            IEnumerable<IdentityRef> teamMembers = teamClient.GetTeamMembers(_projectName, team.Id.ToString()).Result;

            var reviewers = new List<IdentityRefWithVote>();

            Console.WriteLine($"Members of Team name: {team.Name} Guid: {team.Id}:");
            foreach (var member in teamMembers)
            {
                Console.WriteLine($"{member.DisplayName} Id: {member.Id}");
                reviewers.Add(new IdentityRefWithVote() { Id = member.Id });
            }

            // finally, create a PR
            var pr = _gitClient.CreatePullRequestAsync(new GitPullRequest()
            {
                SourceRefName = $"refs/heads/{branchName}",
                TargetRefName = _repo.DefaultBranch,
                Title = "Add PR due to changes",
                Description = "Add PR due to changes",
                Reviewers = reviewers.ToArray()
            },
                _repo.Id).Result;

            Console.WriteLine("project {0}, repo {1}", _project.Name, _repo.Name);
            Console.WriteLine("{0} (#{1}) {2} -> {3}",
                pr.Title.Substring(0, Math.Min(40, pr.Title.Length)),
                pr.PullRequestId,
                pr.SourceRefName,
                pr.TargetRefName);

            return pr;
        }

        //public List<GitBranchStats> GetBranchStats(string branchName)
        //{
        //    Uri uri = new Uri(_uri);
        //    string personalAccessToken = _personalAccessToken;
        //    string projectId = _project;
        //    VssBasicCredential credentials = new VssBasicCredential("", personalAccessToken);
        //    VssConnection connection = new VssConnection(uri, credentials);
        //    GitHttpClient gitClient = connection.GetClient<GitHttpClient>();
        //    ProjectHttpClient projectClient = connection.GetClient<ProjectHttpClient>();
        //    TeamProjectReference project = projectClient.GetProjects(null, top: 1).Result.FirstOrDefault();

        //    var repo = gitClient.GetRepositoriesAsync(projectId).Result.FirstOrDefault();
        //    string defaultBranchName = repo.DefaultBranch.Substring("refs/heads/".Length);


        //    List<GitRef> branches = gitClient.GetRefsAsync(repo.Id, filter: $"heads/{branchName}").Result;

        //    GitQueryBranchStatsCriteria criteria = new GitQueryBranchStatsCriteria()
        //    {
        //        baseVersionDescriptor = new GitVersionDescriptor
        //        {
        //            VersionType = GitVersionType.Branch,
        //            Version = defaultBranchName,
        //        },
        //        targetVersionDescriptors = branches
        //            .Select(branch => new GitVersionDescriptor()
        //            {
        //                Version = branchName,
        //                VersionType = GitVersionType.Branch,
        //            })
        //            .ToArray()
        //    };

        //    List<GitBranchStats> stats = gitClient.GetBranchStatsBatchAsync(criteria, repo.Id).Result;

        //    Console.WriteLine("project {0}, repo {1}", project.Name, repo.Name);
        //    foreach (GitBranchStats stat in stats)
        //    {
        //        Console.WriteLine(" branch `{0}` is {1} ahead, {2} behind `{3}`",
        //            stat.Name, stat.AheadCount, stat.BehindCount, defaultBranchName);
        //        if (stat.AheadCount > 0)
        //        {
        //            Console.WriteLine($"{stat.Name} has changes");
        //        }
        //    }

        //    return stats;
        //}

        public void DeleteBranch(string branchName)
        {
            Console.WriteLine($"Delete branch: '{branchName}'");
            
            List<GitRef> branches = _gitClient.GetRefsAsync(_repo.Id, filter: $"heads/{branchName}").Result;

            // delete the branch
            GitRefUpdateResult refDeleteResult = _gitClient.UpdateRefsAsync(
                new GitRefUpdate[] { new GitRefUpdate() {
                    OldObjectId = branches[0].ObjectId,
                    NewObjectId = new string('0', 40),
                    Name = branchName,
                } },
                repositoryId: _repo.Id).Result.First();

            Console.WriteLine("deleted branch {0} (success={1} status={2})", refDeleteResult.Name, refDeleteResult.Success, refDeleteResult.UpdateStatus);
        }

        public List<FileDiff> GetFileDiffs(GitCommitRef branchCommit)
        {
            Console.WriteLine($"GetFileDiffs from master commit compared to branchCommit: '{branchCommit.CommitId}'");

            //List<GitRef> branches = gitClient.GetRefsAsync(repo.Id, filter: $"heads/{branchCommit.}").Result;
            string defaultBranchName = _repo.DefaultBranch.Substring("refs/heads/".Length);

            var baseCommitRefs = _gitClient.GetCommitsAsync(_repo.Id, new GitQueryCommitsCriteria()
            {
                ItemVersion = new GitVersionDescriptor()
                {
                    VersionType = GitVersionType.Branch,
                    VersionOptions = GitVersionOptions.FirstParent,
                    Version = defaultBranchName
                }
            }).Result;

            var baseCommitRef = baseCommitRefs.FirstOrDefault();

            //API-Endpoint still in preview
            var fileDiffs = _gitClient.GetFileDiffsAsync(new FileDiffsCriteria()
            {
                BaseVersionCommit = baseCommitRef.CommitId,
                FileDiffParams = new List<FileDiffParams>()
                {
                    new FileDiffParams() { OriginalPath = "/xyz.md", Path = "/xyz.md" },
                    new FileDiffParams() {OriginalPath = "/test2.txt", Path="/test2.txt"}
                },
                TargetVersionCommit = branchCommit.CommitId
            }, _project.Id.ToString(), _repo.Id.ToString());

            return fileDiffs.Result;
        }
    }
}
