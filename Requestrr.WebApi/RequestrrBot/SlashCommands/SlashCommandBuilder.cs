using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Requestrr.WebApi.RequestrrBot.ChatClients.Discord;
using Requestrr.WebApi.RequestrrBot.DownloadClients;
using Requestrr.WebApi.RequestrrBot.DownloadClients.Overseerr;
using Requestrr.WebApi.RequestrrBot.DownloadClients.Radarr;
using Requestrr.WebApi.RequestrrBot.DownloadClients.Sonarr;
using Requestrr.WebApi.RequestrrBot.Locale;

namespace Requestrr.WebApi.RequestrrBot
{
    public static class SlashCommandBuilder
    {
        public static string DLLFileName = "slashcommandsbuilder";

        public static Type Build(ILogger logger, DiscordSettings settings, RadarrSettingsProvider radarrSettingsProvider, SonarrSettingsProvider sonarrSettingsProvider, OverseerrSettingsProvider overseerrSettingsProvider)
        {
            string code = GetCode(settings, radarrSettingsProvider.Provide(), sonarrSettingsProvider.Provide(), overseerrSettingsProvider.Provide());
            var tree = SyntaxFactory.ParseSyntaxTree(code);
            string fileName = $"{DLLFileName}-{Guid.NewGuid()}.dll";

            var references = new List<PortableExecutableReference>()
            {
              MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
              MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).GetTypeInfo().Assembly.Location),
              MetadataReference.CreateFromFile(typeof(ApplicationCommandModule).GetTypeInfo().Assembly.Location),
              MetadataReference.CreateFromFile(typeof(SlashCommandBuilder).GetTypeInfo().Assembly.Location),
              MetadataReference.CreateFromFile(typeof(Attribute).GetTypeInfo().Assembly.Location),
              MetadataReference.CreateFromFile(typeof(Task).GetTypeInfo().Assembly.Location),
              MetadataReference.CreateFromFile(typeof(DiscordUser).GetTypeInfo().Assembly.Location),
              MetadataReference.CreateFromFile(typeof(IServiceProvider).GetTypeInfo().Assembly.Location),
              MetadataReference.CreateFromFile(typeof(ILogger).GetTypeInfo().Assembly.Location),
            };

            references.Add(MetadataReference.CreateFromFile(AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "netstandard").Location));

            Assembly.GetEntryAssembly().GetReferencedAssemblies().ToList()
                       .ForEach(a => references.Add(MetadataReference.CreateFromFile(Assembly.Load(a).Location)));

            var compilation = CSharpCompilation.Create(fileName)
              .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
              .AddReferences(references)
              .AddSyntaxTrees(tree);

            string tmpDirectory = string.Empty;
            var dirInfo = new DirectoryInfo(Directory.GetCurrentDirectory());

            try
            {
                tmpDirectory = dirInfo.EnumerateDirectories().Where(x => x.Name == "tmp").Single().FullName;
                if(tmpDirectory == string.Empty || tmpDirectory == null)
                {
                    throw new Exception("tmp folder cannot be found");
                }
            }
            catch
            {
                logger.LogWarning("No tmp folder found, creating one.");
                Directory.CreateDirectory("tmp");
                dirInfo = new DirectoryInfo(Directory.GetCurrentDirectory());
                tmpDirectory = dirInfo.EnumerateDirectories().Where(x => x.Name == "tmp").Single().FullName;
            }

            string path = Path.Combine(tmpDirectory, fileName);

            var compilationResult = compilation.Emit(path);
            if (compilationResult.Success)
            {
                var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
                return asm.GetType("Requestrr.WebApi.RequestrrBot.SlashCommands");
            }
            else
            {
                foreach (Diagnostic codeIssue in compilationResult.Diagnostics)
                {
                    string issue = $"ID: {codeIssue.Id}, Message: {codeIssue.GetMessage()}, Location: { codeIssue.Location.GetLineSpan()},Severity: { codeIssue.Severity}";
                    logger.LogError("Failed to build SlashCommands assembly: " + issue);
                }
            }

            throw new Exception("Failed to build SlashCommands assembly.");
        }

        private static string GetCode(DiscordSettings settings, RadarrSettings radarrSettings, SonarrSettings sonarrSettings, OverseerrSettings overseerrSettings)
        {
            var code = File.ReadAllText("SlashCommands.txt");

            code = code.Replace("[REQUEST_GROUP_NAME]", Language.Current.DiscordCommandRequestGroupName);
            code = code.Replace("[REQUEST_GROUP_DESCRIPTION]", Language.Current.DiscordCommandRequestGroupDescription);
            code = code.Replace("[REQUEST_MOVIE_TITLE_DESCRIPTION]", Language.Current.DiscordCommandMovieRequestTitleDescription);
            code = code.Replace("[REQUEST_MOVIE_TITLE_OPTION_NAME]", Language.Current.DiscordCommandMovieRequestTitleOptionName);
            code = code.Replace("[REQUEST_MOVIE_TITLE_OPTION_DESCRIPTION]", Language.Current.DiscordCommandMovieRequestTitleOptionDescription);
            code = code.Replace("[REQUEST_MOVIE_TMDB_DESCRIPTION]", Language.Current.DiscordCommandMovieRequestTmbdDescription);
            code = code.Replace("[REQUEST_MOVIE_TMDB_OPTION_NAME]", Language.Current.DiscordCommandMovieRequestTmbdOptionName);
            code = code.Replace("[REQUEST_MOVIE_TMDB_OPTION_DESCRIPTION]", Language.Current.DiscordCommandMovieRequestTmbdOptionDescription);
            code = code.Replace("[REQUEST_TV_TITLE_DESCRIPTION]", Language.Current.DiscordCommandTvRequestTitleDescription);
            code = code.Replace("[REQUEST_TV_TITLE_OPTION_NAME]", Language.Current.DiscordCommandTvRequestTitleOptionName);
            code = code.Replace("[REQUEST_TV_TITLE_OPTION_DESCRIPTION]", Language.Current.DiscordCommandTvRequestTitleOptionDescription);
            code = code.Replace("[REQUEST_TV_TVDB_DESCRIPTION]", Language.Current.DiscordCommandTvRequestTvdbDescription);
            code = code.Replace("[REQUEST_TV_TVDB_OPTION_NAME]", Language.Current.DiscordCommandTvRequestTvdbOptionName);
            code = code.Replace("[REQUEST_TV_TVDB_OPTION_DESCRIPTION]", Language.Current.DiscordCommandTvRequestTvdbOptionDescription);
            code = code.Replace("[REQUEST_PING_NAME]", Language.Current.DiscordCommandPingRequestName);
            code = code.Replace("[REQUEST_PING_DESCRIPTION]", Language.Current.DiscordCommandPingRequestDescription);
            code = code.Replace("[REQUEST_HELP_NAME]", Language.Current.DiscordCommandHelpRequestName);
            code = code.Replace("[REQUEST_HELP_DESCRIPTION]", Language.Current.DiscordCommandHelpRequestDescription);
            code = code.Replace("[REQUIRED_MOVIE_ROLE_IDS]", string.Join(",", settings.MovieRoles.Select(x => $"{x}UL")));
            code = code.Replace("[REQUIRED_TV_ROLE_IDS]", string.Join(",", settings.TvShowRoles.Select(x => $"{x}UL")));
            code = code.Replace("[REQUIRED_CHANNEL_IDS]", string.Join(",", settings.MonitoredChannels.Select(x => $"{x}UL")));


            //Issue command handling

            code = code.Replace("[ISSUE_GROUP_NAME]", Language.Current.DiscordCommandIssueName);
            code = code.Replace("[ISSUE_GROUP_DESCRIPTION]", Language.Current.DiscordCommandIssueDescription);

            
            code = code.Replace("[ISSUE_MOVIE_TITLE_DESCRIPTION]", Language.Current.DiscordCommandMovieIssueTitleDescription);
            code = code.Replace("[ISSUE_MOVIE_TITLE_OPTION_NAME]", Language.Current.DiscordCommandMovieIssueTitleOptionName);
            code = code.Replace("[ISSUE_MOVIE_TITLE_OPTION_DESCRIPTION]", Language.Current.DiscordCommandMovieIssueTitleOptionDescription);

            code = code.Replace("[ISSUE_MOVIE_TMDB_DESCRIPTION]", Language.Current.DiscordCommandMovieIssueTmdbDescription);
            code = code.Replace("[ISSUE_MOVIE_TMDB_OPTION_NAME]", Language.Current.DiscordCommandMovieIssueTmdbOptionName);
            code = code.Replace("[ISSUE_MOVIE_TMDB_OPTION_DESCRIPTION]", Language.Current.DiscordCommandMovieIssueTmdbOptionDescription);

            code = code.Replace("[ISSUE_TV_TITLE_DESCRIPTION]", Language.Current.DiscordCommandTvIssueTitleDescription);
            code = code.Replace("[ISSUE_TV_TITLE_OPTION_NAME]", Language.Current.DiscordCommandTvIssueTitleOptionName);
            code = code.Replace("[ISSUE_TV_TITLE_OPTION_DESCRIPTION]", Language.Current.DiscordCommandTvIssueTitleOptionDescription);

            code = code.Replace("[ISSUE_TV_TVDB_DESCRIPTION]", Language.Current.DiscordCommandTvIssueTvdbDescription);
            code = code.Replace("[ISSUE_TV_TVDB_OPTION_NAME]", Language.Current.DiscordCommandTvIssueTvdbOptionName);
            code = code.Replace("[ISSUE_TV_TVDB_OPTION_DESCRIPTION]", Language.Current.DiscordCommandTvIssueTvdbOptionDescription);


            if (settings.MovieDownloadClient == DownloadClient.Disabled && settings.TvShowDownloadClient == DownloadClient.Disabled)
            {
                var beginIndex = code.IndexOf("[REQUEST_COMMAND_START]");
                var endIndex = code.IndexOf("[REQUEST_COMMAND_END]") + "[REQUEST_COMMAND_END]".Length;

                code = code.Replace(code.Substring(beginIndex, endIndex - beginIndex), string.Empty);
            }
            else
            {
                if (settings.MovieDownloadClient == DownloadClient.Disabled)
                {
                    var beginIndex = code.IndexOf("[MOVIE_COMMAND_START]");
                    var endIndex = code.IndexOf("[MOVIE_COMMAND_END]") + "[MOVIE_COMMAND_END]".Length;

                    code = code.Replace(code.Substring(beginIndex, endIndex - beginIndex), string.Empty);
                }
                else if (settings.MovieDownloadClient == DownloadClient.Radarr)
                {
                    code = GenerateMovieCategories(radarrSettings.Categories.Select(x => new Category { Id = x.Id, Name = x.Name }).ToArray(), code);
                }
                else if (settings.MovieDownloadClient == DownloadClient.Overseerr && overseerrSettings.Movies.Categories.Any())
                {
                    code = GenerateMovieCategories(overseerrSettings.Movies.Categories.Select(x => new Category { Id = x.Id, Name = x.Name }).ToArray(), code);
                }
                else
                {
                    code = code.Replace("[REQUEST_MOVIE_TITLE_NAME]", Language.Current.DiscordCommandMovieRequestTitleName);
                    code = code.Replace("[REQUEST_MOVIE_TMDB_NAME]", Language.Current.DiscordCommandMovieRequestTmbdName);
                    code = code.Replace("[MOVIE_COMMAND_START]", string.Empty);
                    code = code.Replace("[MOVIE_COMMAND_END]", string.Empty);
                    code = code.Replace("[TMDB_COMMAND_START]", string.Empty);
                    code = code.Replace("[TMDB_COMMAND_END]", string.Empty);
                    code = code.Replace("[MOVIE_CATEGORY_ID]", "99999");
                }

                if (settings.TvShowDownloadClient == DownloadClient.Disabled)
                {
                    var beginIndex = code.IndexOf("[TV_COMMAND_START]");
                    var endIndex = code.IndexOf("[TV_COMMAND_END]") + "[TV_COMMAND_END]".Length;

                    code = code.Replace(code.Substring(beginIndex, endIndex - beginIndex), string.Empty);
                }
                else if (settings.TvShowDownloadClient == DownloadClient.Sonarr)
                {
                    code = GenerateTvShowCategories(sonarrSettings.Categories.Select(x => new Category { Id = x.Id, Name = x.Name }).ToArray(), code);
                }
                else if (settings.TvShowDownloadClient == DownloadClient.Overseerr && overseerrSettings.TvShows.Categories.Any())
                {
                    code = GenerateTvShowCategories(overseerrSettings.TvShows.Categories.Select(x => new Category { Id = x.Id, Name = x.Name }).ToArray(), code);
                }
                else
                {
                    code = code.Replace("[REQUEST_TV_TITLE_NAME]", Language.Current.DiscordCommandTvRequestTitleName);
                    code = code.Replace("[REQUEST_TV_TVDB_NAME]", Language.Current.DiscordCommandTvRequestTvdbName);
                    code = code.Replace("[TV_COMMAND_START]", string.Empty);
                    code = code.Replace("[TV_COMMAND_END]", string.Empty);
                    code = code.Replace("[TVDB_COMMAND_START]", string.Empty);
                    code = code.Replace("[TVDB_COMMAND_END]", string.Empty);
                    code = code.Replace("[TV_CATEGORY_ID]", "99999");
                }

                code = code.Replace("[REQUEST_COMMAND_START]", string.Empty);
                code = code.Replace("[REQUEST_COMMAND_END]", string.Empty);
            }


            //Handle the removal of Issues if not needed

            if(
                (settings.MovieDownloadClient == DownloadClient.Disabled && settings.TvShowDownloadClient == DownloadClient.Disabled) ||
                (settings.MovieDownloadClient != DownloadClient.Overseerr && settings.TvShowDownloadClient != DownloadClient.Overseerr) ||
                (!overseerrSettings.UseMovieIssue && !overseerrSettings.UseTVIssue)
            )
            {
                //If movies and tv clients disabled, remove the commands
                //Or if download clients is not Overseerr for both, remove.
                //Or if overseerr does not have issues enabled for both TV and Movies, remove.
                int beginIndex = code.IndexOf("[ISSUE_COMMAND_START]");
                int endIndex = code.IndexOf("[ISSUE_COMMAND_END]") + "[ISSUE_COMMAND_END]".Length;

                code = code.Replace(code.Substring(beginIndex, endIndex - beginIndex), string.Empty);
            }
            else
            {
                code = code.Replace("[ISSUE_COMMAND_START]", string.Empty);
                code = code.Replace("[ISSUE_COMMAND_END]", string.Empty);

                if (!overseerrSettings.UseMovieIssue || settings.MovieDownloadClient != DownloadClient.Overseerr)
                {
                    //If download client does not have movies, remove movies
                    int beginIndex = code.IndexOf("[ISSUE_MOVIE_COMMAND_START]");
                    int endIndex = code.IndexOf("[ISSUE_MOVIE_COMMAND_END]") + "[ISSUE_MOVIE_COMMAND_END]".Length;

                    code = code.Replace(code.Substring(beginIndex, endIndex - beginIndex), string.Empty);
                }
                else if (overseerrSettings.UseMovieIssue && settings.MovieDownloadClient == DownloadClient.Overseerr && overseerrSettings.Movies.Categories.Any())
                {
                    code = GenerateMovieIssueCategories(overseerrSettings.Movies.Categories.Select(x => new Category { Id = x.Id, Name = x.Name }).ToArray(), code);
                }

                if(!overseerrSettings.UseTVIssue || settings.TvShowDownloadClient != DownloadClient.Overseerr)
                {
                    //If client does not have TV issues enabled, remove tv
                    int beginIndex = code.IndexOf("[ISSUE_TV_COMMAND_START]");
                    int endIndex = code.IndexOf("[ISSUE_TV_COMMAND_END]") + "[ISSUE_TV_COMMAND_END]".Length;

                    code = code.Replace(code.Substring(beginIndex, endIndex - beginIndex), string.Empty);
                }
                else if (overseerrSettings.UseTVIssue && settings.TvShowDownloadClient == DownloadClient.Overseerr && overseerrSettings.TvShows.Categories.Any())
                {
                    code = GenerateTvShowIssueCategories(overseerrSettings.TvShows.Categories.Select(x => new Category { Id = x.Id, Name = x.Name }).ToArray(), code);
                }

                code = code.Replace("[ISSUE_MOVIE_TITLE_NAME]", Language.Current.DiscordCommandMovieIssueTitleName);
                code = code.Replace("[ISSUE_MOVIE_TMDB_NAME]", Language.Current.DiscordCommandMovieIssueTmdbName);

                code = code.Replace("[ISSUE_TV_TITLE_NAME]", Language.Current.DiscordCommandTvIssueTitleName);
                code = code.Replace("[ISSUE_TV_TVDB_NAME]", Language.Current.DiscordCommandTvIssueTvdbName);

                code = code.Replace("[ISSUE_MOVIE_COMMAND_START]", string.Empty);
                code = code.Replace("[ISSUE_MOVIE_COMMAND_END]", string.Empty);
                code = code.Replace("[ISSUE_TMDB_COMMAND_START]", string.Empty);
                code = code.Replace("[ISSUE_TMDB_COMMAND_END]", string.Empty);

                code = code.Replace("[ISSUE_TV_COMMAND_START]", string.Empty);
                code = code.Replace("[ISSUE_TV_COMMAND_END]", string.Empty);
                code = code.Replace("[ISSUE_TVDB_COMMAND_START]", string.Empty);
                code = code.Replace("[ISSUE_TVDB_COMMAND_END]", string.Empty);
            }

            return code;
        }

        private static string GenerateMovieCategories(Category[] categories, string code)
        {
            var beginIndex = code.IndexOf("[MOVIE_COMMAND_START]");
            var endIndex = code.IndexOf("[MOVIE_COMMAND_END]") + "[MOVIE_COMMAND_END]".Length;
            var categoryCommandTemplate = code.Substring(beginIndex, endIndex - beginIndex);
            categoryCommandTemplate = categoryCommandTemplate.Replace("[MOVIE_COMMAND_START]", string.Empty);
            categoryCommandTemplate = categoryCommandTemplate.Replace("[MOVIE_COMMAND_END]", string.Empty);

            var tmdbStartIndex = categoryCommandTemplate.IndexOf("[TMDB_COMMAND_START]");
            var tmdbEndIndex = categoryCommandTemplate.IndexOf("[TMDB_COMMAND_END]") + "[TMDB_COMMAND_END]".Length;
            categoryCommandTemplate = categoryCommandTemplate.Replace(categoryCommandTemplate.Substring(tmdbStartIndex, tmdbEndIndex - tmdbStartIndex), string.Empty);

            var sb = new StringBuilder();

            foreach (var category in categories)
            {
                var currentTemplate = categoryCommandTemplate;
                currentTemplate = currentTemplate.Replace("[MOVIE_CATEGORY_ID]", category.Id.ToString());
                currentTemplate = currentTemplate.Replace("[REQUEST_MOVIE_TITLE_NAME]", category.Name);
                currentTemplate = currentTemplate.Replace("[REQUEST_MOVIE_TMDB_NAME]", $"{category.Name}-tmdb");

                sb.Append(currentTemplate);
            }

            return code.Replace(code.Substring(beginIndex, endIndex - beginIndex), sb.ToString());
        }


        private static string GenerateMovieIssueCategories(Category[] categories, string code)
        {
            var beginIndex = code.IndexOf("[ISSUE_MOVIE_COMMAND_START]");
            var endIndex = code.IndexOf("[ISSUE_MOVIE_COMMAND_END]") + "[ISSUE_MOVIE_COMMAND_END]".Length;
            var categoryCommandTemplate = code.Substring(beginIndex, endIndex - beginIndex);
            categoryCommandTemplate = categoryCommandTemplate.Replace("[ISSUE_MOVIE_COMMAND_START]", string.Empty);
            categoryCommandTemplate = categoryCommandTemplate.Replace("[ISSUE_MOVIE_COMMAND_END]", string.Empty);

            var tmdbStartIndex = categoryCommandTemplate.IndexOf("[ISSUE_TMDB_COMMAND_START]");
            var tmdbEndIndex = categoryCommandTemplate.IndexOf("[ISSUE_TMDB_COMMAND_END]") + "[ISSUE_TMDB_COMMAND_END]".Length;
            categoryCommandTemplate = categoryCommandTemplate.Replace(categoryCommandTemplate.Substring(tmdbStartIndex, tmdbEndIndex - tmdbStartIndex), string.Empty);

            var sb = new StringBuilder();

            foreach (var category in categories)
            {
                var currentTemplate = categoryCommandTemplate;
                currentTemplate = currentTemplate.Replace("[MOVIE_CATEGORY_ID]", category.Id.ToString());
                currentTemplate = currentTemplate.Replace("[ISSUE_MOVIE_TITLE_NAME]", category.Name);
                currentTemplate = currentTemplate.Replace("[ISSUE_MOVIE_TMDB_NAME]", $"{category.Name}-tmdb");

                sb.Append(currentTemplate);
            }

            return code.Replace(code.Substring(beginIndex, endIndex - beginIndex), sb.ToString());
        }


        private static string GenerateTvShowCategories(Category[] categories, string code)
        {
            var beginIndex = code.IndexOf("[TV_COMMAND_START]");
            var endIndex = code.IndexOf("[TV_COMMAND_END]") + "[TV_COMMAND_END]".Length;
            var categoryCommandTemplate = code.Substring(beginIndex, endIndex - beginIndex);
            categoryCommandTemplate = categoryCommandTemplate.Replace("[TV_COMMAND_START]", string.Empty);
            categoryCommandTemplate = categoryCommandTemplate.Replace("[TV_COMMAND_END]", string.Empty);

            var tvdbStartIndex = categoryCommandTemplate.IndexOf("[TVDB_COMMAND_START]");
            var tvdbEndIndex = categoryCommandTemplate.IndexOf("[TVDB_COMMAND_END]") + "[TVDB_COMMAND_END]".Length;
            categoryCommandTemplate = categoryCommandTemplate.Replace(categoryCommandTemplate.Substring(tvdbStartIndex, tvdbEndIndex - tvdbStartIndex), string.Empty);

            var sb = new StringBuilder();

            foreach (var category in categories)
            {
                var currentTemplate = categoryCommandTemplate;
                currentTemplate = currentTemplate.Replace("[TV_CATEGORY_ID]", category.Id.ToString());
                currentTemplate = currentTemplate.Replace("[REQUEST_TV_TITLE_NAME]", category.Name);
                currentTemplate = currentTemplate.Replace("[REQUEST_TV_TVDB_NAME]", $"{category.Name}-tvdb");

                sb.Append(currentTemplate);
            }

            return code.Replace(code.Substring(beginIndex, endIndex - beginIndex), sb.ToString());
        }


        private static string GenerateTvShowIssueCategories(Category[] categories, string code)
        {
            var beginIndex = code.IndexOf("[ISSUE_TV_COMMAND_START]");
            var endIndex = code.IndexOf("[ISSUE_TV_COMMAND_END]") + "[ISSUE_TV_COMMAND_END]".Length;
            var categoryCommandTemplate = code.Substring(beginIndex, endIndex - beginIndex);
            categoryCommandTemplate = categoryCommandTemplate.Replace("[ISSUE_TV_COMMAND_START]", string.Empty);
            categoryCommandTemplate = categoryCommandTemplate.Replace("[ISSUE_TV_COMMAND_END]", string.Empty);

            var tvdbStartIndex = categoryCommandTemplate.IndexOf("[ISSUE_TVDB_COMMAND_START]");
            var tvdbEndIndex = categoryCommandTemplate.IndexOf("[ISSUE_TVDB_COMMAND_END]") + "[ISSUE_TVDB_COMMAND_END]".Length;
            categoryCommandTemplate = categoryCommandTemplate.Replace(categoryCommandTemplate.Substring(tvdbStartIndex, tvdbEndIndex - tvdbStartIndex), string.Empty);

            var sb = new StringBuilder();

            foreach (var category in categories)
            {
                var currentTemplate = categoryCommandTemplate;
                currentTemplate = currentTemplate.Replace("[TV_CATEGORY_ID]", category.Id.ToString());
                currentTemplate = currentTemplate.Replace("[ISSUE_TV_TITLE_NAME]", category.Name);
                currentTemplate = currentTemplate.Replace("[ISSUE_TV_TVDB_NAME]", $"{category.Name}-tvdb");

                sb.Append(currentTemplate);
            }

            return code.Replace(code.Substring(beginIndex, endIndex - beginIndex), sb.ToString());
        }


        public static void CleanUp()
        {
            try
            {
                var dirInfo = new DirectoryInfo(Directory.GetCurrentDirectory());
                var tmpDirectory = dirInfo.EnumerateDirectories().Where(x => x.Name == "tmp").Single().FullName;
                var filesToDelete = Directory.GetFiles(tmpDirectory, $"*.dll");

                foreach (var dllToDelete in filesToDelete.Where(x => x.Contains(SlashCommandBuilder.DLLFileName, StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        File.Delete(dllToDelete);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private class Category
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }
    }
}