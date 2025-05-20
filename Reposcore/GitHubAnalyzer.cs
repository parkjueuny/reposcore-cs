using Octokit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class GitHubAnalyzer
{
    private readonly GitHubClient _client;

    public GitHubAnalyzer(string token)
    {
        _client = CreateClient("reposcore-cs", token);
    }

    private GitHubClient CreateClient(string productName, string token)
    {
        var client = new GitHubClient(new ProductHeaderValue(productName));

        if (!string.IsNullOrEmpty(token))
        {
            client.Credentials = new Credentials(token);
        }

        return client;
    }

    private void HandleError(Exception ex)
    {
        Console.WriteLine($"❗ 알 수 없는 오류가 발생했습니다: {ex.Message}");
        Environment.Exit(1);
    }

    public void Analyze(string owner, string repo, string outputDir, List<string> formats)
    {
        try
        {
            Console.WriteLine("📥 Pull Requests 로딩 중...");
            var prs = _client.PullRequest.GetAllForRepository(owner, repo, new PullRequestRequest
            {
                State = ItemStateFilter.Closed
            }).Result;

            Console.WriteLine("📥 Issues 로딩 중...");
            var issues = _client.Issue.GetAllForRepository(owner, repo, new RepositoryIssueRequest
            {
                State = ItemStateFilter.All
            }).Result;

            Console.WriteLine("🔍 라벨 통계 분석 중...");
            var targetLabels = new[] { "bug", "documentation", "enhancement" };
            var labelCounts = targetLabels.ToDictionary(label => label, _ => 0);

            foreach (var pr in prs.Where(p => p.Merged == true))
            {
                var labels = pr.Labels.Select(l => l.Name.ToLower()).ToList();
                foreach (var label in targetLabels)
                {
                    if (labels.Contains(label))
                        labelCounts[label]++;
                }
            }

            foreach (var issue in issues)
            {
                if (issue.PullRequest != null) continue;
                var labels = issue.Labels.Select(l => l.Name.ToLower()).ToList();
                foreach (var label in targetLabels)
                {
                    if (labels.Contains(label))
                        labelCounts[label]++;
                }
            }

            Console.WriteLine("\n📊 GitHub Label 통계 결과");

            Console.WriteLine("\n✅ Pull Requests (Merged)");
            foreach (var label in targetLabels)
            {
                Console.WriteLine($"- {char.ToUpper(label[0]) + label.Substring(1)} PRs: {labelCounts[label]}");
            }

            Console.WriteLine("\n✅ Issues");
            foreach (var label in targetLabels)
            {
                Console.WriteLine($"- {char.ToUpper(label[0]) + label.Substring(1)} Issues: {labelCounts[label]}");
            }

            GenerateOutputFiles(outputDir, formats);
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
            HandleError(ex);
        }
    }

    private void GenerateOutputFiles(string outputDir, List<string> formats)
    {
        try
        {
            Directory.CreateDirectory(outputDir);

            foreach (var format in formats)
            {
                // 파일 이름으로 유효한 문자만 사용되었는지 확인
                if (format.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    Console.WriteLine($"❗ 출력 형식 '{format}'에 유효하지 않은 문자가 포함되어 있어 파일을 생성할 수 없습니다.");
                    Environment.Exit(1);
                }

                string fileName = $"result.{format.ToLower()}";
                string filePath = Path.Combine(outputDir, fileName);

                File.WriteAllText(filePath, string.Empty);
                Console.WriteLine($"📁 생성된 파일: {filePath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❗ 출력 파일 생성 중 오류: {ex.Message}");
        }
    }
}
