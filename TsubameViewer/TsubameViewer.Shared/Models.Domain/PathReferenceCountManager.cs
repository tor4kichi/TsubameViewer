using LiteDB;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TsubameViewer.Models.Infrastructure;

namespace TsubameViewer.Models.Domain
{
    public sealed class PathReferenceCountManager 
    {
        public class PathReferenceAddedEventArgs
        {
            public string Path { get; set; }
        }

        public class PathReferenceAddedEvent : PubSubEvent<PathReferenceAddedEventArgs>
        { }

        public class PathReferenceRemovedEventArgs
        {
            public string Path { get; set; }
        }

        public class PathReferenceRemovedEvent : PubSubEvent<PathReferenceRemovedEventArgs>
        { }


        public sealed class PathReferenceCountRepository : LiteDBServiceBase<PathReferenceCountEntry>
        {
            public PathReferenceCountRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
            {
                _collection.EnsureIndex(x => x.ReferenceTokens);
            }

            public bool Exists(string path)
            {
                return _collection.Exists(x => x.Path == path);
            }


            public void Upsert(string path, string token, Action<PathReferenceCountEntry> addNew)
            {
                var entry = _collection.FindById(path);
                if (entry != null)
                {
                    if (!entry.ReferenceTokens.Contains(token))
                    {
                        entry.ReferenceTokens.Add(token);
                    }
                    _collection.Update(entry);
                }
                else
                {
                    _collection.Upsert(entry = new PathReferenceCountEntry() { Path = path, ReferenceTokens = { token } });
                    addNew(entry);
                }
            }

            public void Remove(string token, Action<PathReferenceCountEntry> deleted)
            {
                var items = _collection.Find(x => x.ReferenceTokens.Contains(token)).ToList();
                foreach (var item in items)
                {
                    item.ReferenceTokens.Remove(token);
                    if (item.ReferenceTokens.Count == 0)
                    {
                        _collection.Delete(item.Path);
                        deleted(item);
                    }
                    else
                    {
                        _collection.Update(item);
                    }
                }
            }


            public string GetToken(string path)
            {
                var item = _collection.FindById(path);
                return item?.ReferenceTokens.FirstOrDefault();
            }

            public string[] GetTokens(string path)
            {
                return _collection.FindById(path)?.ReferenceTokens.ToArray();
            }
        }

        private readonly PathReferenceCountRepository _pathReferenceCountRepository;
        private readonly IEventAggregator _eventAggregator;

        PathReferenceRemovedEvent _removedEvent;
        PathReferenceAddedEvent _addedEvent;

        public PathReferenceCountManager(
            PathReferenceCountRepository pathReferenceCountRepository,
            IEventAggregator eventAggregator
            )
        {
            _pathReferenceCountRepository = pathReferenceCountRepository;
            _eventAggregator = eventAggregator;
            _addedEvent = _eventAggregator.GetEvent<PathReferenceAddedEvent>();
            _removedEvent = _eventAggregator.GetEvent<PathReferenceRemovedEvent>();
            
        }


        public bool Exist(string path)
        {
            return _pathReferenceCountRepository.Exists(path);
        }


        public void Upsert(string path, string token)
        {
            _pathReferenceCountRepository.Upsert(path, token, (item) => 
            {
                _addedEvent.Publish(new PathReferenceAddedEventArgs() { Path = item.Path });
            });
        }

        public void Remove(string token)
        {
            _pathReferenceCountRepository.Remove(token, (item) => 
            {
                _removedEvent.Publish(new PathReferenceRemovedEventArgs() { Path = item.Path });
            });
        }


        public string GetToken(string path)
        {
            return _pathReferenceCountRepository.GetToken(path);
        }

        public string[] GetTokens(string path)
        {
            return _pathReferenceCountRepository.GetTokens(path);
        }

        public sealed class PathReferenceCountEntry
        {
            [BsonId]
            public string Path { get; set; }

            [BsonField]
            public List<string> ReferenceTokens { get; set; } = new List<string>();
        }
    }

}
