using System;
using System.Configuration;
using System.Linq;
using Autofac;
using AutofacSerilogIntegration;
using Microsoft.Extensions.Configuration;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Serilog;

namespace AZDevops_POC
{
    class Program
    {
        static void Main(string[] args)
        {
            var branches = new Branches();
            var branchName = $"Test123-{DateTime.UtcNow.Ticks}";

            var gitCreateBranchAndPushChanges = branches.CreateBranchAndPushChanges(branchName);
            var newBranchCommit = gitCreateBranchAndPushChanges.Commits.FirstOrDefault();
            
            var fileDiffChecks = branches.GetFileDiffs(newBranchCommit);

            var hasFileChanges = fileDiffChecks.SelectMany(f => f.LineDiffBlocks.Where(l => l.ChangeType != LineDiffBlockChangeType.None)).Any();
            if (hasFileChanges)
            {
                var createPr = branches.CreatePullRequest(branchName);
            }
            else
            {
                branches.DeleteBranch(branchName);
            }

            // Logic to create a branch and a Pull Request:
            // 1. Get current Repo-Folder-Files which should be checked
            // 2. Compare current Repo-Folder-Files with Files which will be added or updated in the current request by name-comparison
            //      1. New file added? => Create PR
            //      2. FileChanges on existing files? => Create PR



            // ToDos:
            // - Clarify Authentication for this application
            // - How to get Configs from Target.Hub
            // - How to do Continuous Delivery back into Target.Hub
            // - Which kind of application to use, recurring WebJob or Azure Function as a WebHook?
        }
    }
}
