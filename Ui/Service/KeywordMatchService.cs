using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Shawn.Utils;
using VariableKeywordMatcher.Model;
using VariableKeywordMatcher.Provider.ChineseZhCnPinYin;
using VariableKeywordMatcher.Provider.ChineseZhCnPinYinInitials;
using VariableKeywordMatcher.Provider.DirectMatch;
using VariableKeywordMatcher.Provider.DiscreteMatch;
using VariableKeywordMatcher.Provider.InitialsMatch;

namespace _1RM.Service
{
    public class MatchProviderInfo : NotifyPropertyChangedBase
    {

        private string _name = "";
        public string Name
        {
            get => _name;
            set => SetAndNotifyIfChanged(ref _name, value);
        }


        private string _title1 = "";
        public string Title1
        {
            get => _title1;
            set => SetAndNotifyIfChanged(ref _title1, value);
        }



        private string _title2 = "";
        public string Title2
        {
            get => _title2;
            set => SetAndNotifyIfChanged(ref _title2, value);
        }


        private bool _enabled = true;
        public bool Enabled
        {
            get => _enabled;
            set => SetAndNotifyIfChanged(ref _enabled, value);
        }

        private bool _isEditable = true;
        public bool IsEditable
        {
            get => _isEditable;
            set => SetAndNotifyIfChanged(ref _isEditable, value);
        }
    }

    public class KeywordMatchService
    {
        private const int MaxCacheEntries = 1024;
        private static readonly TimeSpan CacheTrimThreshold = TimeSpan.FromHours(6);
        private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(2);

        public class Cache
        {
            public Cache(MatchCache matchCache)
            {
                _matchCache = matchCache;
                _accessTime = DateTime.Now;
            }

            private DateTime _accessTime;
            private MatchCache _matchCache;

            public ref MatchCache GetMatchCache()
            {
                _accessTime = DateTime.Now;
                return ref _matchCache;
            }

            public DateTime GetAccessTime() => _accessTime;
        }

        /// <summary>
        /// this is the cache for the raw string to speed up match, for example, a english srint "Abc Def" will be cached as {"abc def", "ad"} a Chinese name will be cache as PinYin.
        /// </summary>
        private readonly Dictionary<string, Cache> _matchCaches = new Dictionary<string, Cache>(500);

        private VariableKeywordMatcher.Matcher _matcher;

        public KeywordMatchService()
        {
            _matcher = VariableKeywordMatcherIn1.Builder.Build(new string[]
            {
                DirectMatchProvider.GetName(),
                DiscreteMatchProvider.GetName(),
            }, false);
        }


        public void Init(string[] providerNames)
        {
            Debug.Assert(providerNames.Length > 0);
            _matcher = VariableKeywordMatcherIn1.Builder.Build(providerNames, false);
            _matchCaches.Clear();
        }

        private void CleanUp()
        {
            lock (this)
            {
                var now = DateTime.Now;
                var shouldTrimByAge = _matchCaches.Any(x => now - x.Value.GetAccessTime() > CacheTrimThreshold);
                var shouldTrimByCount = _matchCaches.Count > MaxCacheEntries;
                if (!shouldTrimByAge && !shouldTrimByCount)
                {
                    return;
                }

                var expiredKeys = _matchCaches
                    .Where(x => now - x.Value.GetAccessTime() > CacheExpiration)
                    .OrderBy(x => x.Value.GetAccessTime())
                    .Select(x => x.Key)
                    .ToArray();
                foreach (var key in expiredKeys)
                {
                    _matchCaches.Remove(key);
                }

                if (_matchCaches.Count > MaxCacheEntries)
                {
                    var overflowKeys = _matchCaches
                        .OrderBy(x => x.Value.GetAccessTime())
                        .Take(_matchCaches.Count - MaxCacheEntries)
                        .Select(x => x.Key)
                        .ToArray();
                    foreach (var key in overflowKeys)
                    {
                        _matchCaches.Remove(key);
                    }
                }
            }
        }

        //public MatchResult Match(string originalString, List<string> keywords)
        //{
        //    CleanUp();
        //    var cache = GetCache(originalString);
        //    return _matcher.Match(cache, keywords);
        //}

        //public MatchResult Match(string originalString, string keyword)
        //{
        //    CleanUp();
        //    var cache = GetCache(originalString);
        //    return _matcher.Match(cache, new[] { keyword });
        //}

        public MatchResults Match(List<string> originalStrings, IEnumerable<string> keywords)
        {
            var kws = keywords.ToArray();
            CleanUp();
            var matchCaches = originalStrings.Select(x => GetCache(x)).ToList();
            return _matcher.Matchs(matchCaches, kws, 2);
        }

        private ref MatchCache GetCache(string originalString)
        {
            lock (this)
            {
                if (!_matchCaches.ContainsKey(originalString))
                {
                    var cache = new MatchCache(originalString);
                    _matchCaches.Add(originalString, new Cache(cache));
                } 
                return ref _matchCaches[originalString].GetMatchCache();
            }
        }

        public void UpdateMatchCache(string originalString)
        {
            var newCache = _matcher.CreateStringCache(originalString);
            GetCache(originalString).SpellCaches = newCache.SpellCaches;
        }

        public static List<MatchProviderInfo> GetMatchProviderInfos()
        {
            var providerNames = VariableKeywordMatcherIn1.Builder.GetAvailableProviderNames();
            var matchProviderInfos = new List<MatchProviderInfo>(Enumerable.Count<string>(providerNames));
            foreach (var enumProviderType in providerNames)
            {
                matchProviderInfos.Add(new MatchProviderInfo()
                {
                    Name = enumProviderType,
                    Title1 = VariableKeywordMatcherIn1.Builder.GetProviderDescription(enumProviderType),
                    Title2 = VariableKeywordMatcherIn1.Builder.GetProviderDescriptionEn(enumProviderType),
                    Enabled = false,
                });
            }
            // first time init.
            var ci = CultureInfo.CurrentCulture;
            string code = ci.Name.ToLower();
            foreach (var matchProviderInfo in matchProviderInfos)
            {
                matchProviderInfo.Enabled = false;
            }

            var setEnabled = new Action<string, bool, bool>((name, isEnabled, isEditable) =>
            {
                if (matchProviderInfos.Any(x => x.Name == name))
                {
                    matchProviderInfos.First(x => x.Name == name).Enabled = isEnabled;
                    matchProviderInfos.First(x => x.Name == name).IsEditable = isEditable;
                }
            });

            setEnabled(DirectMatchProvider.GetName(), true, false);
            setEnabled(InitialsMatchProvider.GetName(), true, true);
            setEnabled(DiscreteMatchProvider.GetName(), false, true);

            if (code.StartsWith("zh"))
            {
                setEnabled(ChineseZhCnPinYinMatchProvider.GetName(), true, true);
                setEnabled(ChineseZhCnPinYinInitialsMatchProvider.GetName(), true, true);
            }
            return matchProviderInfos;
        }
    }
}
