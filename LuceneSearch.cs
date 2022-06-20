using System;
using System.Collections.Generic;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Lucene.Net.Analysis.Standard;
using System.Web.Hosting;
using System.Linq;
using System.Text;
using System.Globalization;

namespace WebApplication1
{
    public class LuceneSearch
    {
        //some variables
        private static LuceneVersion version = LuceneVersion.LUCENE_48;
        private static string path = HostingEnvironment.ApplicationPhysicalPath + "/LuceneIndex";


        /// <summary>
        /// Create some dummy data, but the real data could be coming from a database, file or other (external) source
        /// </summary>
        /// <returns></returns>
        public static List<SearchIndexItem> CreateDummyData()
        {
            //the ID will be used to grab the correct source data when searching
            return new List<SearchIndexItem>()
            {
                new SearchIndexItem()
                {
                    ID = 1,
                    title = "Lorem Ipsum",
                    contents = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. VDWWD ."
                },
                new SearchIndexItem()
                {
                    ID = 2,
                    title = "Bootstrap",
                    contents = "Quickly design and customize responsive mobile-first sites with Bootstrap, the world’s most popular front-end open source toolkit, featuring Sass variables and mixins, responsive grid system, extensive prebuilt components, and powerful JavaScript plugins. VDWWD."
                },
                new SearchIndexItem()
                {
                    ID = 3,
                    title = "Lucene.Net",
                    contents = "Lucene.Net is a port of the Lucene search engine library, written in C# and targeted at .NET runtime users. VDWWD."
                },
                new SearchIndexItem()
                {
                    ID = 4,
                    title = "Weird Characters",
                    contents = "Thè qüick brôwn fox jùmps õver thë låzy døg. VDWWD."
                },
            };
        }


        /// <summary>
        /// Create a searchable index with Lucene. Run this on Application Start or on scheduled intervals with something like Quartz.Net
        /// </summary>
        /// <param name="source">List of data to be indexed</param>
        public static void CreateSearchIndex(List<SearchIndexItem> source)
        {
            var analyzer = new StandardAnalyzer(version);
            var config = new IndexWriterConfig(version, analyzer);

            using (var folder = FSDirectory.Open(path))
            using (var index = new IndexWriter(FSDirectory.Open(path), config))
            {
                //delete the current index first
                //you could also update instead of delete and recreate but then you will have to keep track of the changes in the source data. If you do remove the line below.
                index.DeleteAll();

                foreach (var item in source)
                {
                    var doc = new Document();

                    //add the id to the index
                    var id = new TextField("ID", item.ID.ToString(), Field.Store.YES);
                    
                    //increase or decrease the importance of the search result hit on this field by adjusting the Boost value
                    id.Boost = 0.5F;
                    doc.Add(id);

                    //add the title to the index
                    if (!string.IsNullOrEmpty(item.title))
                    {
                        var title = new TextField("title", item.title, Field.Store.YES);
                        title.Boost = 2F;
                        doc.Add(title);
                    }

                    //add the contents to the index
                    if (!string.IsNullOrEmpty(item.contents))
                    {
                        //do not store the data in the search index for this field
                        var tf = new TextField("contents", item.contents, Field.Store.NO);
                        
                        tf.Boost = 1F;
                        doc.Add(tf);
                    }

                    //add the document to the index
                    index.AddDocument(doc);

                    //or update the index
                    //var term = new Term("ID", item.ID.ToString());
                    //index.UpdateDocument(term, doc);
                }

                //write the index to disk
                index.Flush(triggerMerge: true, applyAllDeletes: false);
                index.Commit();
            }
        }


        /// <summary>
        /// Search for the searh term in the stored indexes
        /// </summary>
        /// <param name="SearchTerm">What to search for</param>
        /// <param name="TotalResults">How many hits should be returned</param>
        /// <returns></returns>
        public static List<SearchResult> StartSearch(string SearchTerm, int TotalResults)
        {
            var results = new List<SearchResult>();

            //is the search term not too short or empty
            if (string.IsNullOrEmpty(SearchTerm) || SearchTerm.Length < 3)
                return results;

            //make sure at least some results are returned
            if (TotalResults < 3)
                TotalResults = 3;

            //clean the search term
            SearchTerm = CleanSearchTerm(SearchTerm);

            //open the index from disk
            using (var folder = FSDirectory.Open(path))
            using (var reader = DirectoryReader.Open(folder))
            {
                var searcher = new IndexSearcher(reader);
                var query = new BooleanQuery();

                //split the search term in separate words
                var parts = SearchTerm.Split(' ');

                //search for all the parts
                foreach (var item in parts)
                {
                    //search
                    query.Add(new TermQuery(new Term("ID", item)), Occur.SHOULD);
                    query.Add(new TermQuery(new Term("title", item)), Occur.SHOULD);
                    query.Add(new TermQuery(new Term("contents", item)), Occur.SHOULD);
                    
                    //search with fuzzy logic
                    query.Add(new FuzzyQuery(new Term("ID", item)), Occur.SHOULD);
                    query.Add(new FuzzyQuery(new Term("title", item)), Occur.SHOULD);
                    query.Add(new FuzzyQuery(new Term("contents", item)), Occur.SHOULD);

                    string wildcard = $"*{item}*";

                    //search with wildcard
                    query.Add(new WildcardQuery(new Term("ID", wildcard)), Occur.SHOULD);
                    query.Add(new WildcardQuery(new Term("title", wildcard)), Occur.SHOULD);
                    query.Add(new WildcardQuery(new Term("contents", wildcard)), Occur.SHOULD);
                }

                //the list of search results
                var hits = searcher.Search(query, TotalResults).ScoreDocs;

                //loop all the results to get the contents
                foreach (var hit in hits)
                {
                    var document = searcher.Doc(hit.Doc);

                    //get the title and id from the search index
                    var searchhit = new SearchResult()
                    {
                        ID = Convert.ToInt32(document.Get("ID")),
                        title = document.Get("title"),
                        score = hit.Score
                    };

                    //get the contents from the source
                    var contents = CreateDummyData().Where(x => x.ID == searchhit.ID).FirstOrDefault();

                    if (contents != null)
                    {
                        searchhit.contents = contents.contents;
                    }

                    //add the hit to the results list
                    results.Add(searchhit);
                }
            }

            //return the results
            return results;
        }


        /// <summary>
        /// Cleanup the search term and replace special characters with normal ones (é > e, ä > a etc)
        /// </summary>
        /// <param name="SearchTerm">The search term to be cleaned</param>
        /// <returns></returns>
        public static string CleanSearchTerm(string SearchTerm)
        {
            if (string.IsNullOrEmpty(SearchTerm))
                return SearchTerm;

            //replace double spaces and the asterix
            SearchTerm = SearchTerm.Trim().Replace("  ", " ").Replace("*", "");

            //replace special characters
            var decomposed = SearchTerm.Normalize(NormalizationForm.FormD);
            var filtered = decomposed.Where(c => char.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);

            //return the new string
            return new string(filtered.ToArray()).ToLower();
        }


        public class SearchIndexItem
        {
            public int ID { get; set; }
            public string title { get; set; }
            public string contents { get; set; }
        }


        public class SearchResult : SearchIndexItem
        {
            public float score { get; set; }
        }
    }
}
