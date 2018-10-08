using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Carvers.Infra.Extensions;

namespace Carvers.Infra
{
    public static class CsvReader
    {
        private static IEnumerable<T> Read<T>(string text, Func<string[], T> ctr, int skipCount)
        {
            return text
                .AsLines()
                .Skip(skipCount) //for header
                .Select(line => ctr(line.AsCsv()))
                .Where(item => item != null);
        }

        public static async Task<IEnumerable<T>> ReadFileAsync<T>(FileInfo path, Func<string[], T> ctr, int skip)
        {
            return await Task<IEnumerable<T>>.Factory.StartNew(() => ReadFile(path, ctr, skip));
        }

        public static IEnumerable<T> ReadFile<T>(FileInfo file, Func<string[], T> ctr, int skip)
        {
            return Read(File.ReadAllText(file.FullName), ctr, skip);
        }

        public static IEnumerable<T> ReadColumn<T>(FileInfo file, int number)
        {
            return ReadFile(file, values => values[number], skip: 0)
                .Select(symbol => symbol.Replace("\r", string.Empty))
                .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
                .Cast<T>();
        }
    }

    public class FileFeed
    {
        private Tuple<bool,string> peekedLine;
        private readonly StreamReader fileReader;

        public FileFeed(string filePath)
        {
            peekedLine = Tuple.Create<bool, string>(false, null);
            fileReader = new StreamReader(new FileStream(filePath, FileMode.Open, FileAccess.Read));
        }

        public string ReadNextLine()
        {
            if (!peekedLine.Item1)
            {
                return fileReader.EndOfStream ? string.Empty : fileReader.ReadLine();
            }

            var lineToReturn = peekedLine.Item2;
            peekedLine = Tuple.Create<bool, string>(false, null);
            return lineToReturn;
        }

        public string PeekLine()
        {
            if (peekedLine.Item1)
                return peekedLine.Item2;

            peekedLine = Tuple.Create(true, ReadNextLine());
            return peekedLine.Item2;
        }
    }

    public class FileFeed<T>
        where T: class
    {
        private readonly Func<string, T> ctr;
        private Tuple<bool, T> peekedLine;
        private readonly StreamReader fileReader;

        public FileFeed(string filePath, Func<string, T> ctr)
        {
            this.ctr = ctr;
            peekedLine = Tuple.Create<bool, T>(false, null);
            fileReader = new StreamReader(new FileStream(filePath, FileMode.Open, FileAccess.Read));
        }

        public T ReadNextLine()
        {
            if (!peekedLine.Item1)
            {
                return fileReader.EndOfStream ? null : ctr(fileReader.ReadLine());
            }

            var lineToReturn = peekedLine.Item2;
            peekedLine = Tuple.Create<bool, T>(false, null);
            return lineToReturn;
        }

        public T PeekLine()
        {
            if (peekedLine.Item1)
                return peekedLine.Item2;

            peekedLine = Tuple.Create(true, ReadNextLine());
            return peekedLine.Item2;
        }
    }

    public class FileFeedService<T>
        where T : class
    {
        private readonly Subject<T> stream;

        public FileFeedService(FileInfo file, Func<string, T> ctr)
            : this(file, ctr, null)
        {
        }

        public FileFeedService(FileInfo file, Func<string, T> ctr, TimeSpan? streamSpeed)
        {
            stream = new Subject<T>();

            var feed = new FileFeed<T>(file.FullName, ctr);

            //Observable
            //    .Interval(streamSpeed)
            //    .Subscribe(_ =>
            //    {
            //        stream.Publish(feed.ReadNextLine());
            //    });

            if (streamSpeed.HasValue)
            {
                Stream = Observable
                    .Interval(streamSpeed.Value)
                    .Select(val => feed.ReadNextLine());
            }
            else
            {
                Stream = stream;

                Task.Factory.StartNew(() =>
                {
                    

                    while (true)
                    {
                        stream.OnNext(feed.ReadNextLine());


                    //var toSend = fileFeeds
                    //.Select(feed => Tuple.Create(feed, feed.PeekLine()))
                    //.OrderBy(tup => tup.Item2.Timestamp)
                    //.First()
                    //.Item1.ReadNextLine();

                    //if (toSend.Timestamp.UnixEpochToLocalTime() >= startTime)
                    //    realTimeBarMsgSubject.OnNext(toSend);
                    }
                });
            }

        }

        public IObservable<T> Stream { get; private set; }
    }
    
}