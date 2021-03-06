using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ServiceStack.DataAnnotations;
using ServiceStack.IO;
using ServiceStack.Script;
using ServiceStack.Text;

namespace ServiceStack.Script
{
    // ReSharper disable InconsistentNaming
    
    public class ProtectedScripts : ScriptMethods
    {
        public MemoryVirtualFiles vfsMemory() => new MemoryVirtualFiles();

        public FileSystemVirtualFiles vfsFileSystem(string dirPath) => new FileSystemVirtualFiles(dirPath);
        
        public GistVirtualFiles vfsGist(string gistId) => new GistVirtualFiles(gistId);
        public GistVirtualFiles vfsGist(string gistId, string accessToken) => new GistVirtualFiles(gistId, accessToken);

        public IVirtualFile ResolveFile(string filterName, ScriptScopeContext scope, string virtualPath)
        {
            var file = ResolveFile(scope.Context.VirtualFiles, scope.PageResult.VirtualPath, virtualPath);
            if (file == null)
                throw new FileNotFoundException($"{filterName} '{virtualPath}' in page '{scope.Page.VirtualPath}' was not found");

            return file;
        }

        public IVirtualFile ResolveFile(IVirtualPathProvider virtualFiles, string fromVirtualPath, string virtualPath)
        {
            IVirtualFile file = null;

            var pathMapKey = nameof(ResolveFile) + ">" + fromVirtualPath;
            var pathMapping = Context.GetPathMapping(pathMapKey, virtualPath);
            if (pathMapping != null)
            {
                file = virtualFiles.GetFile(pathMapping);
                if (file != null)
                    return file;                    
                Context.RemovePathMapping(pathMapKey, pathMapping);
            }

            var tryExactMatch = virtualPath.IndexOf('/') >= 0; //if nested path specified, look for an exact match first
            if (tryExactMatch)
            {
                file = virtualFiles.GetFile(virtualPath);
                if (file != null)
                {
                    Context.SetPathMapping(pathMapKey, virtualPath, virtualPath);
                    return file;
                }
            }

            var parentPath = fromVirtualPath.IndexOf('/') >= 0
                ? fromVirtualPath.LastLeftPart('/')
                : "";

            do
            {
                var seekPath = parentPath.CombineWith(virtualPath);
                file = virtualFiles.GetFile(seekPath);
                if (file != null)
                {
                    Context.SetPathMapping(pathMapKey, virtualPath, seekPath);
                    return file;
                }

                if (parentPath == "")
                    break;

                parentPath = parentPath.IndexOf('/') >= 0
                    ? parentPath.LastLeftPart('/')
                    : "";
            } while (true);

            return null;
        }

        //alias
        public Task fileContents(ScriptScopeContext scope, string virtualPath) => includeFile(scope, virtualPath);

        public async Task includeFile(ScriptScopeContext scope, string virtualPath)
        {
            var file = ResolveFile(nameof(includeFile), scope, virtualPath);
            using (var reader = file.OpenRead())
            {
                await reader.CopyToAsync(scope.OutputStream);
            }
        }

        public async Task ifDebugIncludeScript(ScriptScopeContext scope, string virtualPath)
        {
            if (scope.Context.DebugMode)
            {
                await scope.OutputStream.WriteAsync("<script>");
                await includeFile(scope, virtualPath);
                await scope.OutputStream.WriteAsync("</script>");
            }
        }

        IVirtualPathProvider VirtualFiles => Context.VirtualFiles;
        
        // Old Aliases for Backwards compatibility
        [Alias("allFiles")]
        public IEnumerable<IVirtualFile> vfsAllFiles() => allFiles(VirtualFiles);
        [Alias("allRootFiles")]
        public IEnumerable<IVirtualFile> vfsAllRootFiles() => allRootFiles(VirtualFiles);
        [Alias("allRootDirectories")]
        public IEnumerable<IVirtualDirectory> vfsAllRootDirectories() => allRootDirectories(VirtualFiles);
        [Alias("combinePath")]
        public string vfsCombinePath(string basePath, string relativePath) => combinePath(VirtualFiles, basePath, relativePath);
        [Alias("findFilesInDirectory")]
        public IEnumerable<IVirtualFile> dirFilesFind(string dirPath, string globPattern) => findFilesInDirectory(VirtualFiles,dirPath,globPattern);
        [Alias("findFiles")]
        public IEnumerable<IVirtualFile> filesFind(string globPattern) => findFiles(VirtualFiles,globPattern);
        [Alias("writeFile")]
        public string fileWrite(string virtualPath, object contents) => writeFile(VirtualFiles, virtualPath, contents);
        [Alias("appendToFile")]
        public string fileAppend(string virtualPath, object contents) => appendToFile(VirtualFiles, virtualPath, contents);
        [Alias("deleteFile")]
        public string fileDelete(string virtualPath) => deleteFile(VirtualFiles, virtualPath);
        [Alias("deleteFile")]
        public string dirDelete(string virtualPath) => deleteFile(VirtualFiles, virtualPath);
        [Alias("fileTextContents")]
        public string fileReadAll(string virtualPath) => fileTextContents(VirtualFiles,virtualPath);
        [Alias("fileBytesContent")]
        public byte[] fileReadAllBytes(string virtualPath) => fileBytesContent(VirtualFiles, virtualPath);
        
        public IEnumerable<IVirtualFile> allFiles() => allFiles(VirtualFiles);
        public IEnumerable<IVirtualFile> allFiles(IVirtualPathProvider vfs) => vfs.GetAllFiles();

        public IEnumerable<IVirtualFile> allRootFiles() => allRootFiles(VirtualFiles);
        public IEnumerable<IVirtualFile> allRootFiles(IVirtualPathProvider vfs) => vfs.GetRootFiles();
        public IEnumerable<IVirtualDirectory> allRootDirectories() => allRootDirectories(VirtualFiles);
        public IEnumerable<IVirtualDirectory> allRootDirectories(IVirtualPathProvider vfs) => vfs.GetRootDirectories();
        public string combinePath(string basePath, string relativePath) => combinePath(VirtualFiles, basePath, relativePath);
        public string combinePath(IVirtualPathProvider vfs, string basePath, string relativePath) => vfs.CombineVirtualPath(basePath, relativePath);

        public IVirtualDirectory dir(string virtualPath) => dir(VirtualFiles,virtualPath);
        public IVirtualDirectory dir(IVirtualPathProvider vfs, string virtualPath) => vfs.GetDirectory(virtualPath);
        public bool dirExists(string virtualPath) => VirtualFiles.DirectoryExists(virtualPath);
        public bool dirExists(IVirtualPathProvider vfs, string virtualPath) => vfs.DirectoryExists(virtualPath);
        public IVirtualFile dirFile(string dirPath, string fileName) => dirFile(VirtualFiles,dirPath,fileName);
        public IVirtualFile dirFile(IVirtualPathProvider vfs, string dirPath, string fileName) => vfs.GetDirectory(dirPath)?.GetFile(fileName);
        public IEnumerable<IVirtualFile> dirFiles(string dirPath) => dirFiles(VirtualFiles,dirPath);
        public IEnumerable<IVirtualFile> dirFiles(IVirtualPathProvider vfs, string dirPath) => vfs.GetDirectory(dirPath)?.GetFiles() ?? new List<IVirtualFile>();
        public IVirtualDirectory dirDirectory(string dirPath, string dirName) => dirDirectory(VirtualFiles,dirPath,dirName);
        public IVirtualDirectory dirDirectory(IVirtualPathProvider vfs, string dirPath, string dirName) => vfs.GetDirectory(dirPath)?.GetDirectory(dirName);
        public IEnumerable<IVirtualDirectory> dirDirectories(string dirPath) => dirDirectories(VirtualFiles,dirPath);
        public IEnumerable<IVirtualDirectory> dirDirectories(IVirtualPathProvider vfs, string dirPath) => vfs.GetDirectory(dirPath)?.GetDirectories() ?? new List<IVirtualDirectory>();
        public IEnumerable<IVirtualFile> findFilesInDirectory(string dirPath, string globPattern) => findFilesInDirectory(VirtualFiles,dirPath,globPattern);
        public IEnumerable<IVirtualFile> findFilesInDirectory(IVirtualPathProvider vfs, string dirPath, string globPattern) => vfs.GetDirectory(dirPath)?.GetAllMatchingFiles(globPattern);

        public IEnumerable<IVirtualFile> findFiles(string globPattern) => findFiles(VirtualFiles,globPattern);
        public IEnumerable<IVirtualFile> findFiles(IVirtualPathProvider vfs, string globPattern) => vfs.GetAllMatchingFiles(globPattern);
        public bool fileExists(string virtualPath) => fileExists(VirtualFiles,virtualPath);
        public bool fileExists(IVirtualPathProvider vfs, string virtualPath) => vfs.FileExists(virtualPath);
        public IVirtualFile file(string virtualPath) => file(VirtualFiles,virtualPath);
        public IVirtualFile file(IVirtualPathProvider vfs, string virtualPath) => vfs.GetFile(virtualPath);
        public string writeFile(string virtualPath, object contents) => writeFile(VirtualFiles, virtualPath, contents);
        public string writeFile(IVirtualPathProvider vfs, string virtualPath, object contents)
        {
            if (contents is string s)
                vfs.WriteFile(virtualPath, s);
            else if (contents is byte[] bytes)
                vfs.WriteFile(virtualPath, bytes);
            else if (contents is Stream stream)
                vfs.WriteFile(virtualPath, stream);
            else
                return null;

            return virtualPath;
        }

        public object writeTextFiles(IVirtualPathProvider vfs, Dictionary<string,string> textFiles)
        {
            vfs.WriteFiles(textFiles);
            return IgnoreResult.Value;
        }

        public string appendToFile(string virtualPath, object contents) => appendToFile(VirtualFiles, virtualPath, contents);
        public string appendToFile(IVirtualPathProvider vfs, string virtualPath, object contents)
        {
            if (contents is string s)
                vfs.AppendFile(virtualPath, s);
            else if (contents is byte[] bytes)
                vfs.AppendFile(virtualPath, bytes);
            else if (contents is Stream stream)
                vfs.AppendFile(virtualPath, stream);
            else
                return null;

            return virtualPath;
        }

        public string deleteFile(string virtualPath) => deleteFile(VirtualFiles, virtualPath);
        public string deleteFile(IVirtualPathProvider vfs, string virtualPath)
        {
            vfs.DeleteFile(virtualPath);
            return virtualPath;
        }

        public string deleteDirectory(string virtualPath) => deleteFile(VirtualFiles, virtualPath);
        public string deleteDirectory(IVirtualPathProvider vfs, string virtualPath)
        {
            vfs.DeleteFolder(virtualPath);
            return virtualPath;
        }

        public string fileTextContents(string virtualPath) => fileTextContents(VirtualFiles,virtualPath);
        public string fileTextContents(IVirtualPathProvider vfs, string virtualPath) => vfs.GetFile(virtualPath)?.ReadAllText();
        public string textContents(IVirtualFile file) => file?.ReadAllText();
        public byte[] fileBytesContent(string virtualPath) => fileBytesContent(VirtualFiles, virtualPath);
        public byte[] fileBytesContent(IVirtualPathProvider vfs, string virtualPath) => vfs.GetFile(virtualPath)?.ReadAllBytes();
        public byte[] bytesContent(IVirtualFile file) => file?.ReadAllBytes();
        public string fileHash(string virtualPath) => fileHash(VirtualFiles,virtualPath);
        public string fileHash(IVirtualPathProvider vfs, string virtualPath) => vfs.GetFileHash(virtualPath);
        public string fileHash(IVirtualFile file) => file?.GetFileHash();

        //alias
        public Task urlContents(ScriptScopeContext scope, string url) => includeUrl(scope, url, null);
        public Task urlContents(ScriptScopeContext scope, string url, object options) => includeUrl(scope, url, options);

        public Task includeUrl(ScriptScopeContext scope, string url) => includeUrl(scope, url, null);
        public async Task includeUrl(ScriptScopeContext scope, string url, object options)
        {
            var scopedParams = scope.AssertOptions(nameof(includeUrl), options);

            var webReq = (HttpWebRequest)WebRequest.Create(url);
            var dataType = scopedParams.TryGetValue("dataType", out object value)
                ? ConvertDataTypeToContentType((string)value)
                : null;

            if (scopedParams.TryGetValue("method", out value))
                webReq.Method = (string)value;
            if (scopedParams.TryGetValue("contentType", out value) || dataType != null)
                webReq.ContentType = (string)value ?? dataType;            
            if (scopedParams.TryGetValue("accept", out value) || dataType != null) 
                webReq.Accept = (string)value ?? dataType;            
            if (scopedParams.TryGetValue("userAgent", out value))
                PclExport.Instance.SetUserAgent(webReq, (string)value);

            if (scopedParams.TryRemove("data", out object data))
            {
                if (webReq.Method == null)
                    webReq.Method = HttpMethods.Post;
                    
                if (webReq.ContentType == null)
                    webReq.ContentType = MimeTypes.FormUrlEncoded;

                var body = ConvertDataToString(data, webReq.ContentType);
                using (var stream = await webReq.GetRequestStreamAsync())
                {
                    await stream.WriteAsync(body);
                }
            }

            using (var webRes = await webReq.GetResponseAsync())
            using (var stream = webRes.GetResponseStream())
            {
                await stream.CopyToAsync(scope.OutputStream);
            }
        }

        private static string ConvertDataTypeToContentType(string dataType)
        {
            switch (dataType)
            {
                case "json":
                    return MimeTypes.Json;
                case "jsv":
                    return MimeTypes.Jsv;
                case "csv":
                    return MimeTypes.Csv;
                case "xml":
                    return MimeTypes.Xml;
                case "text":
                    return MimeTypes.PlainText;
                case "form":
                    return MimeTypes.FormUrlEncoded;
            }
            
            throw new NotSupportedException($"Unknown dataType '{dataType}'");
        }

        private static string ConvertDataToString(object data, string contentType)
        {
            if (data is string s)
                return s;
            switch (contentType)
            {
                case MimeTypes.PlainText:
                    return data.ToString();
                case MimeTypes.Json:
                    return data.ToJson();
                case MimeTypes.Csv:
                    return data.ToCsv();
                case MimeTypes.Jsv:
                    return data.ToJsv();
                case MimeTypes.Xml:
                    return data.ToXml();
                case MimeTypes.FormUrlEncoded:
                    WriteComplexTypeDelegate holdQsStrategy = QueryStringStrategy.FormUrlEncoded;
                    QueryStringSerializer.ComplexTypeStrategy = QueryStringStrategy.FormUrlEncoded;
                    var urlEncodedBody = QueryStringSerializer.SerializeToString(data);
                    QueryStringSerializer.ComplexTypeStrategy = holdQsStrategy;
                    return urlEncodedBody;
            }

            throw new NotSupportedException($"Can not serialize to unknown Content-Type '{contentType}'");
        }

        public static string CreateCacheKey(string url, Dictionary<string,object> options=null)
        {
            var sb = StringBuilderCache.Allocate()
                .Append(url);
            
            if (options != null)
            {
                foreach (var entry in options)
                {
                    sb.Append(entry.Key)
                      .Append('=')
                      .Append(entry.Value);
                }
            }

            return StringBuilderCache.ReturnAndFree(sb);
        }
        
        //alias
        public Task fileContentsWithCache(ScriptScopeContext scope, string virtualPath) => includeFileWithCache(scope, virtualPath, null);
        public Task fileContentsWithCache(ScriptScopeContext scope, string virtualPath, object options) => includeFileWithCache(scope, virtualPath, options);

        public Task includeFileWithCache(ScriptScopeContext scope, string virtualPath) => includeFileWithCache(scope, virtualPath, null);
        public async Task includeFileWithCache(ScriptScopeContext scope, string virtualPath, object options)
        {
            var scopedParams = scope.AssertOptions(nameof(includeUrl), options);
            var expireIn = scopedParams.TryGetValue("expireInSecs", out object value)
                ? TimeSpan.FromSeconds(value.ConvertTo<int>())
                : (TimeSpan)scope.Context.Args[ScriptConstants.DefaultFileCacheExpiry];
            
            var cacheKey = CreateCacheKey("file:" + scope.PageResult.VirtualPath + ">" + virtualPath, scopedParams);
            if (Context.ExpiringCache.TryGetValue(cacheKey, out Tuple<DateTime, object> cacheEntry))
            {
                if (cacheEntry.Item1 > DateTime.UtcNow && cacheEntry.Item2 is byte[] bytes)
                {
                    await scope.OutputStream.WriteAsync(bytes);
                    return;
                }
            }

            var file = ResolveFile(nameof(includeFileWithCache), scope, virtualPath);
            var ms = MemoryStreamFactory.GetStream();
            using (ms)
            {
                using (var reader = file.OpenRead())
                {
                    await reader.CopyToAsync(ms);
                }

                ms.Position = 0;
                var bytes = ms.ToArray();
                Context.ExpiringCache[cacheKey] = Tuple.Create(DateTime.UtcNow.Add(expireIn),(object)bytes);
                await scope.OutputStream.WriteAsync(bytes);
            }
        }

        //alias
        public Task urlContentsWithCache(ScriptScopeContext scope, string url) => includeUrlWithCache(scope, url, null);
        public Task urlContentsWithCache(ScriptScopeContext scope, string url, object options) => includeUrlWithCache(scope, url, options);
        
        public Task includeUrlWithCache(ScriptScopeContext scope, string url) => includeUrlWithCache(scope, url, null);
        public async Task includeUrlWithCache(ScriptScopeContext scope, string url, object options)
        {
            var scopedParams = scope.AssertOptions(nameof(includeUrl), options);
            var expireIn = scopedParams.TryGetValue("expireInSecs", out object value)
                ? TimeSpan.FromSeconds(value.ConvertTo<int>())
                : (TimeSpan)scope.Context.Args[ScriptConstants.DefaultUrlCacheExpiry];

            var cacheKey = CreateCacheKey("url:" + url, scopedParams);
            if (Context.ExpiringCache.TryGetValue(cacheKey, out Tuple<DateTime, object> cacheEntry))
            {
                if (cacheEntry.Item1 > DateTime.UtcNow && cacheEntry.Item2 is byte[] bytes)
                {
                    await scope.OutputStream.WriteAsync(bytes);
                    return;
                }
            }

            var dataType = scopedParams.TryGetValue("dataType", out value)
                ? ConvertDataTypeToContentType((string)value)
                : null;

            if (scopedParams.TryGetValue("method", out value) && !((string)value).EqualsIgnoreCase("GET"))
                throw new NotSupportedException($"Only GET requests can be used in {nameof(includeUrlWithCache)} filters in page '{scope.Page.VirtualPath}'");
            if (scopedParams.TryGetValue("data", out value))
                throw new NotSupportedException($"'data' is not supported in {nameof(includeUrlWithCache)} filters in page '{scope.Page.VirtualPath}'");

            var ms = MemoryStreamFactory.GetStream();
            using (ms)
            {
                var captureScope = scope.ScopeWithStream(ms);
                await includeUrl(captureScope, url, options);

                ms.Position = 0;
                var expireAt = DateTime.UtcNow.Add(expireIn);

                var bytes = ms.ToArray();
                Context.ExpiringCache[cacheKey] = cacheEntry = Tuple.Create(expireAt,(object)bytes);
                await scope.OutputStream.WriteAsync(bytes);
            }
        }
        
        static readonly string[] AllCacheNames = {
            nameof(ScriptContext.Cache),
            nameof(ScriptContext.CacheMemory),
            nameof(ScriptContext.ExpiringCache),
            nameof(SharpPageUtils.BinderCache),
            nameof(ScriptContext.JsTokenCache),
            nameof(ScriptContext.AssignExpressionCache),
            nameof(ScriptContext.PathMappings),
        };

        internal IDictionary GetCache(string cacheName)
        {
            switch (cacheName)
            {
                case nameof(ScriptContext.Cache):
                    return Context.Cache;
                case nameof(ScriptContext.CacheMemory):
                    return Context.CacheMemory;
                case nameof(ScriptContext.ExpiringCache):
                    return Context.ExpiringCache;
                case nameof(SharpPageUtils.BinderCache):
                    return SharpPageUtils.BinderCache;
                case nameof(ScriptContext.JsTokenCache):
                    return Context.JsTokenCache;
                case nameof(ScriptContext.AssignExpressionCache):
                    return Context.AssignExpressionCache;
                case nameof(ScriptContext.PathMappings):
                    return Context.PathMappings;
            }
            return null;
        }

        public object cacheClear(ScriptScopeContext scope, object cacheNames)
        {
            IEnumerable<string> caches;
            if (cacheNames is string strName)
            {
                caches = strName.EqualsIgnoreCase("all")
                    ? AllCacheNames
                    : new[]{ strName };
            }
            else if (cacheNames is IEnumerable<string> nameList)
            {
                caches = nameList;
            }
            else throw new NotSupportedException(nameof(cacheClear) + 
                 " expects a cache name or list of cache names but received: " + (cacheNames.GetType()?.Name ?? "null"));

            int entriesRemoved = 0;
            foreach (var cacheName in caches)
            {
                var cache = GetCache(cacheName);
                if (cache == null)
                    throw new NotSupportedException(nameof(cacheClear) + $": Unknown cache '{cacheName}'");

                entriesRemoved += cache.Count;
                cache.Clear();
            }

            return entriesRemoved;
        }

        public object invalidateAllCaches(ScriptScopeContext scope)
        {
            cacheClear(scope, "all");
            return scope.Context.InvalidateCachesBefore = DateTime.UtcNow;
        }

        public string sh(ScriptScopeContext scope, string arguments) => sh(scope, arguments, null);
        public string sh(ScriptScopeContext scope, string arguments, Dictionary<string, object> options)
        {
            if (string.IsNullOrEmpty(arguments))
                return null;
            
            if (options == null)
                options = new Dictionary<string, object>();

            if (Env.IsWindows)
            {
                options["arguments"] = "/C " + arguments;
                return proc(scope, "cmd.exe", options);
            }
            else
            {
                var escapedArgs = arguments.Replace("\"", "\\\"");
                options["arguments"] = $"-c \"{escapedArgs}\"";
                return proc(scope, "/bin/bash", options);
            }
        }
        
        public string proc(ScriptScopeContext scope, string fileName) => proc(scope, fileName, null);
        public string proc(ScriptScopeContext scope, string fileName, Dictionary<string, object> options)
        {
            var process = new Process
            {
                StartInfo =
                {
                    FileName = fileName,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                }
            };

            if (options.TryGetValue("arguments", out var oArguments))
                process.StartInfo.Arguments = oArguments.AsString();
            
            if (options.TryGetValue("dir", out var oWorkDir))
                process.StartInfo.WorkingDirectory = oWorkDir.AsString();

            try 
            { 
                using (process)
                {
                    process.StartInfo.RedirectStandardError = true;
                    process.Start();

                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();

                    process.WaitForExit();
                    process.Close();

                    if (!string.IsNullOrEmpty(error))
                        throw new Exception($"`{fileName} {process.StartInfo.Arguments}` command failed: " + error);

                    return output;
                }            
            }
            catch (Exception ex)
            {
                throw new StopFilterExecutionException(scope, options, ex);
            }
        }

        public string exePath(string exeName)
        {
            try
            {
                var p = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = false,
                        FileName = Env.IsWindows 
                            ? "where"  //Win 7/Server 2003+
                            : "which", //macOS / Linux
                        Arguments = exeName,
                        RedirectStandardOutput = true
                    }
                };
                p.Start();
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();

                if (p.ExitCode == 0)
                {
                    // just return first match
                    var fullPath = output.Substring(0, output.IndexOf(Environment.NewLine, StringComparison.Ordinal));
                    if (!string.IsNullOrEmpty(fullPath))
                    {
                        return fullPath;
                    }
                }
            }
            catch {}               
            return null;
        }
        
    }
}