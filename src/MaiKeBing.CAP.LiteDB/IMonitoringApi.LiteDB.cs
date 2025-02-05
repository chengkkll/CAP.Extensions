﻿// Copyright (c) .NET Core Community. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DotNetCore.CAP.Internal;
using DotNetCore.CAP.Messages;
using DotNetCore.CAP.Monitoring;
using DotNetCore.CAP.Persistence;
using LiteDB;

namespace MaiKeBing.CAP.LiteDB
{
    internal class LiteDBMonitoringApi : IMonitoringApi
    {
        public Task<MediumMessage> GetPublishedMessageAsync(long id)
        {
            return Task.FromResult((MediumMessage)LiteDBStorage.PublishedMessages.FindOne(x => x.Id == id.ToString(CultureInfo.InvariantCulture)));
        }

        public Task<MediumMessage> GetReceivedMessageAsync(long id)
        {
            return Task.FromResult((MediumMessage)LiteDBStorage.ReceivedMessages.FindOne(x => x.Id == id.ToString(CultureInfo.InvariantCulture)));
        }

        public StatisticsDto GetStatistics()
        {
            var stats = new StatisticsDto
            {
                PublishedSucceeded = LiteDBStorage.PublishedMessages.Count(x => x.StatusName == StatusName.Succeeded),
                ReceivedSucceeded = LiteDBStorage.ReceivedMessages.Count(x => x.StatusName == StatusName.Succeeded),
                PublishedFailed = LiteDBStorage.PublishedMessages.Count(x => x.StatusName == StatusName.Failed),
                ReceivedFailed = LiteDBStorage.ReceivedMessages.Count(x => x.StatusName == StatusName.Failed)
            };
            return stats;
        }

        public IDictionary<DateTime, int> HourlyFailedJobs(MessageType type)
        {
            return GetHourlyTimelineStats(type, nameof(StatusName.Failed));
        }

        public IDictionary<DateTime, int> HourlySucceededJobs(MessageType type)
        {
            return GetHourlyTimelineStats(type, nameof(StatusName.Succeeded));
        }

        public PagedQueryResult<MessageDto> Messages(MessageQueryDto queryDto)
        {
            if (queryDto.MessageType == MessageType.Publish)
            {
                var expression = LiteDBStorage.PublishedMessages.FindAll();
                if (!string.IsNullOrEmpty(queryDto.StatusName))
                {
                    expression = expression.Where(x => x.StatusName.ToString().Equals(queryDto.StatusName, StringComparison.InvariantCultureIgnoreCase));
                }

                if (!string.IsNullOrEmpty(queryDto.Name))
                {
                    expression = expression.Where(x => x.Name.Equals(queryDto.Name, StringComparison.InvariantCultureIgnoreCase));
                }

                if (!string.IsNullOrEmpty(queryDto.Content))
                {
                    expression = expression.Where(x => x.Content.Contains(queryDto.Content));
                }

                var offset = queryDto.CurrentPage * queryDto.PageSize;
                var size = queryDto.PageSize;

                var allItems = expression.Select(x => new MessageDto()
                {
                    Added = x.Added,
                    Version = "N/A",
                    Content = x.Content,
                    ExpiresAt = x.ExpiresAt,
                    Id = x.Id,
                    Name = x.Name,
                    Retries = x.Retries,
                    StatusName = x.StatusName.ToString()
                });

                return new PagedQueryResult<MessageDto>()
                {
                    Items = allItems.Skip(offset).Take(size).ToList(),
                    PageIndex = queryDto.CurrentPage,
                    PageSize = queryDto.PageSize,
                    Totals = allItems.Count()
                };
            }
            else
            {
                var expression = LiteDBStorage.ReceivedMessages.FindAll();

                if (!string.IsNullOrEmpty(queryDto.StatusName))
                {
                    expression = expression.Where(x => x.StatusName.ToString().Equals(queryDto.StatusName, StringComparison.InvariantCultureIgnoreCase));
                }

                if (!string.IsNullOrEmpty(queryDto.Name))
                {
                    expression = expression.Where(x => x.Name.Equals(queryDto.Name, StringComparison.InvariantCultureIgnoreCase));
                }

                if (!string.IsNullOrEmpty(queryDto.Group))
                {
                    expression = expression.Where(x => x.Group.Equals(queryDto.Group, StringComparison.InvariantCultureIgnoreCase));
                }

                if (!string.IsNullOrEmpty(queryDto.Content))
                {
                    expression = expression.Where(x => x.Content.Contains(queryDto.Content));
                }

                var offset = queryDto.CurrentPage * queryDto.PageSize;
                var size = queryDto.PageSize;

                var allItems = expression.Select(x => new MessageDto()
                {
                    Added = x.Added,
                    Group = x.Group,
                    Version = "N/A",
                    Content = x.Content,
                    ExpiresAt = x.ExpiresAt,
                    Id = x.Id,
                    Name = x.Name,
                    Retries = x.Retries,
                    StatusName = x.StatusName.ToString()
                });

                return new PagedQueryResult<MessageDto>()
                {
                    Items = allItems.Skip(offset).Take(size).ToList(),
                    PageIndex = queryDto.CurrentPage,
                    PageSize = queryDto.PageSize,
                    Totals = allItems.Count()
                };
            }
        }

        public int PublishedFailedCount()
        {
            return LiteDBStorage.PublishedMessages.Count(x => x.StatusName == StatusName.Failed);
        }

        public int PublishedSucceededCount()
        {
            return LiteDBStorage.PublishedMessages.Count(x => x.StatusName == StatusName.Succeeded);
        }

        public int ReceivedFailedCount()
        {
            return LiteDBStorage.ReceivedMessages.Count(x => x.StatusName == StatusName.Failed);
        }

        public int ReceivedSucceededCount()
        {
            return LiteDBStorage.ReceivedMessages.Count(x => x.StatusName == StatusName.Succeeded);
        }

        private Dictionary<DateTime, int> GetHourlyTimelineStats(MessageType type, string statusName)
        {
            var endDate = DateTime.Now;
            var dates = new List<DateTime>();
            for (var i = 0; i < 24; i++)
            {
                dates.Add(endDate);
                endDate = endDate.AddHours(-1);
            }

            var keyMaps = dates.ToDictionary(x => x.ToString("yyyy-MM-dd-HH"), x => x);


            Dictionary<string, int> valuesMap;
            if (type == MessageType.Publish)
            {
                valuesMap = LiteDBStorage.PublishedMessages
                    .Find(x => x.StatusName.ToString() == statusName)
                    .GroupBy(x => x.Added.ToString("yyyy-MM-dd-HH"))
                    .ToDictionary(x => x.Key, x => x.Count());
            }
            else
            {
                valuesMap = LiteDBStorage.ReceivedMessages
                    .Find(x => x.StatusName.ToString() == statusName)
                    .GroupBy(x => x.Added.ToString("yyyy-MM-dd-HH"))
                    .ToDictionary(x => x.Key, x => x.Count());
            }

            foreach (var key in keyMaps.Keys)
            {
                if (!valuesMap.ContainsKey(key))
                {
                    valuesMap.Add(key, 0);
                }
            }

            var result = new Dictionary<DateTime, int>();
            for (var i = 0; i < keyMaps.Count; i++)
            {
                var value = valuesMap[keyMaps.ElementAt(i).Key];
                result.Add(keyMaps.ElementAt(i).Value, value);
            }
            return result;
        }
    }
}