using Octokit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Cocona;

public class GitHubAnalyzer
{
    private readonly GitHubClient _client;

    public GitHubAnalyzer()
    {
        _client = new GitHubClient(new ProductHeaderValue("reposcore-cs"));
    }

    public void Analyze(
        string owner,
        string repo,
        [Option("output", Description = "출력 경로. 지정하지 않으면 콘솔에 출력됩니다.")] string output = null,
        [Option("format", Description = "출력 포맷: text 또는 json")] string format = "text"
    )
    {
        try
        {
            var prs = _client.PullRequest.GetAllForRepository(owner, repo, new PullRequestRequest
            {
                State = ItemStateFilter.Closed
            }).Result;

            var issues = _client.Issue.GetAllForRepository(owner, repo, new RepositoryIssueRequest
            {
                State = ItemStateFilter.All
            }).Result;

            var targetLabels = new[] { "bug", "documentation", "enhancement" };

            int pr_bug = 0, pr_doc = 0, pr_feat = 0;
            int issue_bug = 0, issue_doc = 0, issue_feat = 0;

            foreach (var pr in prs.Where(p => p.Merged == true))
            {
                var labels = pr.Labels.Select(l => l.Name.ToLower()).ToList();

                if (labels.Contains("bug")) pr_bug++;
                if (labels.Contains("documentation")) pr_doc++;
                if (labels.Contains("enhancement")) pr_feat++;
            }

            foreach (var issue in issues)
            {
                if (issue.PullRequest != null) continue;

                var labels = issue.Labels.Select(l => l.Name.ToLower()).ToList();

                if (labels.Contains("bug")) issue_bug++;
                if (labels.Contains("documentation")) issue_doc++;
                if (labels.Contains("enhancement")) issue_feat++;
            }

            if (!string.IsNullOrEmpty(output))
            {
                if (format.ToLower() == "json")
                {
                    var result = new
                    {
                        PullRequests = new { Bug = pr_bug, Documentation = pr_doc, Enhancement = pr_feat },
                        Issues = new { Bug = issue_bug, Documentation = issue_doc, Enhancement = issue_feat }
                    };

                    var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(output, json);
                    Console.WriteLine($"📁 결과가 JSON 파일로 저장되었습니다: {output}");
                }
                else
                {
                    using (var writer = new StreamWriter(output))
                    {
                        writer.WriteLine("\n📊 GitHub Label 통계 결과");
                        writer.WriteLine("\n✅ Pull Requests (Merged)");
                        writer.WriteLine($"- Bug PRs: {pr_bug}");
                        writer.WriteLine($"- Documentation PRs: {pr_doc}");
                        writer.WriteLine($"- Enhancement PRs: {pr_feat}");
                        writer.WriteLine("\n✅ Issues");
                        writer.WriteLine($"- Bug Issues: {issue_bug}");
                        writer.WriteLine($"- Documentation Issues: {issue_doc}");
                        writer.WriteLine($"- Enhancement Issues: {issue_feat}");
                    }
                    Console.WriteLine($"📁 결과가 텍스트 파일로 저장되었습니다: {output}");
                }
            }
            else
            {
                Console.WriteLine("\n📊 GitHub Label 통계 결과");

                Console.WriteLine("\n✅ Pull Requests (Merged)");
                Console.WriteLine($"- Bug PRs: {pr_bug}");
                Console.WriteLine($"- Documentation PRs: {pr_doc}");
                Console.WriteLine($"- Enhancement PRs: {pr_feat}");

                Console.WriteLine("\n✅ Issues");
                Console.WriteLine($"- Bug Issues: {issue_bug}");
                Console.WriteLine($"- Documentation Issues: {issue_doc}");
                Console.WriteLine($"- Enhancement Issues: {issue_feat}");
            }
        }
        catch (RateLimitExceededException)
        {
            Console.WriteLine("❗ API 호출 한도(Rate Limit)를 초과했습니다. 잠시 후 다시 시도해주세요.");
            Environment.Exit(1);
        }
        catch (AuthorizationException)
        {
            Console.WriteLine("❗ 인증 실패: 올바른 토큰을 사용했는지 확인하세요.");
            Environment.Exit(1);
        }
        catch (NotFoundException)
        {
            Console.WriteLine("❗ 저장소를 찾을 수 없습니다. owner/repo 이름을 확인하세요.");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❗ 알 수 없는 오류가 발생했습니다: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
