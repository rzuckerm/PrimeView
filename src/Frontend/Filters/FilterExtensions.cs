﻿using PrimeView.Entities;
using PrimeView.Frontend.Pages;
using PrimeView.Frontend.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace PrimeView.Frontend.Filters
{
	public static class FilterExtensions
	{
		private static readonly Func<Result, bool> successfulResult;

		static FilterExtensions()
		{
			Expression<Func<Result, bool>> successfulResultExpression = result => result.Status == null || result.Status == "success";
			successfulResult = successfulResultExpression.Compile();
		}

		public static IEnumerable<Result> Viewable(this IEnumerable<Result> source)
		{
			return source.Where(successfulResult);
		}

		public static IEnumerable<Result> ApplyFilters(this IEnumerable<Result> source, ReportDetails page)
		{
			var filterLanguages = page.FilterLanguages;

			if (filterLanguages.Count > 0)
				source = source.Where(r => filterLanguages.Contains(r.Language));

			var filteredResults = source.Where(r =>
				r.IsMultiThreaded switch
				{
					true => page.FilterParallelMultithreaded,
					_ => page.FilterParallelSinglethreaded
				}
				&& r.Algorithm switch
				{
					"base" => page.FilterAlgorithmBase,
					"wheel" => page.FilterAlgorithmWheel,
					_ => page.FilterAlgorithmOther
				}
				&& r.IsFaithful switch
				{
					true => page.FilterFaithful,
					_ => page.FilterUnfaithful
				}
				&& r.Bits switch
				{
					null => page.FilterBitsUnknown,
					1 => page.FilterBitsOne,
					_ => page.FilterBitsOther
				}
			);

			return page.OnlyHighestPassesPerSecondPerThreadPerLanguage
				? filteredResults
					.GroupBy(r => r.Language)
					.SelectMany(group => group.Where(r => r.PassesPerSecond == group.Max(r => r.PassesPerSecond)))
				: filteredResults;
		}

		public static string CreateSummary(this IResultFilterPropertyProvider filter, ILanguageInfoProvider languageInfoProvider)
		{
            List<string> segments = new()
            {
                filter.FilterLanguages.Count switch
                {
                    0 => "all languages",
                    1 => $"{languageInfoProvider.GetLanguageInfo(filter.FilterLanguages[0]).Name}",
                    _ => $"{filter.FilterLanguages.Count} languages"
                },

                (filter.FilterParallelSinglethreaded, filter.FilterParallelMultithreaded) switch
                {
                    (true, false) => "single-threaded",
                    (false, true) => "multithreaded",
                    _ => null
                },

                (filter.FilterAlgorithmBase, filter.FilterAlgorithmWheel, filter.FilterAlgorithmOther) switch
                {
                    (false, false, false) => null,
                    (true, false, false) => "base algorithm",
                    (false, true, false) => "wheel algorithm",
                    (false, false, true) => "other algorithms",
                    (true, true, true) => "all algorithms",
                    _ => "multiple algorithms"
                },

                (filter.FilterFaithful, filter.FilterUnfaithful) switch
                {
                    (true, false) => "faithful",
                    (false, true) => "unfaithful",
                    _ => null
                },

                (filter.FilterBitsOne, filter.FilterBitsOther, filter.FilterBitsUnknown) switch
                {
                    (true, false, false) => "one bit",
                    (false, true, false) => "multiple bits",
                    (false, false, true) => "unknown bits",
                    (true, true, false) => "known bits",
                    (false, true, true) => "all but one bit",
                    (true, false, true) => "one or unknown bits",
                    _ => null
                }
            };

            return string.Join(", ", segments.Where(s => s != null));
		}

		public static IList<string> SplitFilterValues(this string text)
		{
			return text.Split('~', StringSplitOptions.RemoveEmptyEntries);
		}

		public static string JoinFilterValues(this IEnumerable<string> values)
		{
			return string.Join('~', values);
		}
	}
}
