//
// Lame Blog 1.0
//
// Features:
//    Per-day entries
//    HTML and .txt files supported (pulls header from the file).
//    Include text support
//
//
// Template macros:
//
//	@BLOG_ENTRIES@
//	    The blob entries rendered
//
// TODO:
//   Add images, so I can do:
//   @image file
//   @caption Caption
//

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Globalization;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using Rss;

class DayEntry : IComparable {
	public DateTime Date;
	public string Body;
	public string Caption = "";
	public string DateCaption;
	public string Category = "";
	
	Blog blog;
	string extra = "";
	
	public string blog_base;

	//
	// Text to inline CSS for the blog for classes code, code-csharp and shell
	//
	const string code_style = "class=\"code\" style=\"border-style: solid; background: #ddddff; border-width: 1px; padding: 2pt;\"";
	const string code_csharp_style = "class=\"code-csharp\" style=\"border-style: solid; background: #ddddff; border-width: 1px; padding: 2pt;\"";
	const string shell_style = "style=\"border-style: solid; background: #000000; color: #bbbbbb; border-width: 1px; padding: 2pt;\"";

	//
	// The date when we started using filestamps instead of the hardcoded 4pm
	//
	public DateTime SwitchDate = new DateTime (2005, 05, 25, 0, 0, 8);
	
	DayEntry (Blog blog, string file)
	{
		this.blog = blog;
		blog_base = blog.config.BlogWebDirectory;
		ParseDate (file);

		using (FileStream i = File.OpenRead (file)){
			using (StreamReader s = new StreamReader (i, Encoding.GetEncoding (blog.config.InputEncoding))){
				if (file.EndsWith (".html"))
					Load (s, true);
				else if (file.EndsWith (".txt"))
					Load (s, false);
			}
		}
	}

	public static DayEntry Load (Blog blog, string file)
	{
		DayEntry de = null;
		
		try {
			de = new DayEntry (blog, file);
		} catch {
			Console.WriteLine ("Failed to load file: {0}", file);
		}
		return de;
	}
	
	void ParseDate (string file)
	{
		int month;

		int idx = file.IndexOf (blog.config.BlogDirectory);
		string entry = file;
		if (idx >= 0)
			entry = file.Substring (blog.config.BlogDirectory.Length);
		Match match = Regex.Match (entry, "^(.*/)?(200[0-9])/([a-z]+)-0*([0-9]+)(-[0-9])?");

		Category = match.Groups [1].Value;
		int year = Int32.Parse (match.Groups [2].Value);
		int day = Int32.Parse (match.Groups [4].Value);
		string month_name = match.Groups [3].Value;
		extra = match.Groups [5].Value;

		switch (month_name){
		case "jan":
			month = 1; break;
		case "feb":
			month = 2; break;
		case "mar":
			month = 3; break;
		case "apr":
			month = 4; break;
		case "may":
			month = 5; break;
		case "jun":
			month = 6; break;
		case "jul":
			month = 7; break;
		case "aug":
			month = 8; break;
		case "sep":
			month = 9; break;
		case "oct":
			month = 10; break;
		case "nov":
			month = 11; break;
		case "dec":
			month = 12; break;
		default:
			throw new Exception ("Unknown month: " + month_name + " from: " + file);
		}

		Date = new DateTime (year, month, day, 13, 55, 0);

		//
		// Start using the file time stamp as the publishing date, this is 
		// better than hardcoding a value.  To avoid people sending us long
		// rants, only do this after today
		//
		if (Date > SwitchDate){
			FileInfo fi = new FileInfo (file);
			DateTime access_date = fi.LastWriteTimeUtc;

			Console.WriteLine ("Hour: {0}", access_date.Hour);
			Date = new DateTime (year, month, day, access_date.Hour, access_date.Minute, 0);
		}
					     
		DateCaption = String.Format ("{0:dd} {0:MMM} {0:yyyy}", Date);
	}
	
	void Load (StreamReader i, bool is_html)
	{
		bool caption_found = false;
		bool in_pre = false;
		StringBuilder sb = new StringBuilder ();
		string s;
		
		while ((s = i.ReadLine ()) != null){
			if (!caption_found){
				if (is_html){
					if (s.StartsWith ("<h1>")){
						Caption = s.Replace ("<h1>", "").Replace ("</h1>", "");
						caption_found = true;
						continue;
					} else if (s.StartsWith ("#include")){
						sb.Append (Include (s.Substring (9), out Caption));
						caption_found = true;
						continue;
					}
				} else {
					if (s.StartsWith ("@") && !caption_found){
						Caption = s.Substring (1);
						caption_found = true;
						continue;
					}
				}
			}
			if (!is_html){
				if (s == "" && !in_pre)
					sb.Append ("<p>");
				else if (s.StartsWith ("@"))
					sb.Append (String.Format ("<h1>{0}</h1>", s.Substring (1)));
				else if (s.StartsWith ("#pre"))
					in_pre = true;
				else if (s.StartsWith ("#endpre"))
					in_pre = false;
				else
					sb.Append (s);
			} else {
				if (s.StartsWith ("#include")){
					string c;
					sb.Append (Include (s.Substring (9), out c));
					continue;
				} else if (s.StartsWith ("#pic")){
					int idx = s.IndexOf (",");
					if (idx == -1){
						Console.WriteLine ("Wrong #pic command");
						continue;
					}
					
					string filename = s.Substring (5, idx-5);
					string caption = s.Substring (idx + 1);
					sb.Append (String.Format ("<p><center><a href=\"{0}pic.php?name={1}&caption={2}\"><img border=0 src=\"{3}/pictures/small-{1}\"></a><p>{2}</center></p>", blog_base, filename, caption, blog.config.BlogImageBasedir));
					continue;
					
				}
				sb.Append (s);
			}
			sb.Append ("\n");
		}
		Body = sb.ToString ();
	}

	public int CompareTo (object o)
	{
		return Date.CompareTo (((DayEntry) o).Date);
	}

	string Include (string file, out string caption)
	{
		if (file.StartsWith ("~/")){
			file = Environment.GetEnvironmentVariable ("HOME") + "/" + file.Substring (2);
		}

		string article_file = "./texts/" + Path.GetFileName (file);
		File.Copy (file, article_file, true);
		article_file = article_file.Substring (1);

		//
		// Remove header stuff, and include inline, stick a copy 
		//
		StringBuilder r = new StringBuilder ();

		caption = "";

		Console.WriteLine ("Reading: " + file);
		using (FileStream i = File.OpenRead (file)){
			StreamReader s = new StreamReader (i, Encoding.GetEncoding (blog.config.InputEncoding));
			string line;
			bool output = false;
			
			while ((line = s.ReadLine ()) != null){
				Match m = Regex.Match (line, "<title>(.*)</title>");
				if (m.Groups.Count > 1){
					caption = line.Substring (m.Groups [1].Index, m.Groups [1].Length);
					blog.AddArticle (blog_base + article_file, caption);
					continue;
				}
				if (!output){
					if (line == "<!--start-->"){
						output = true;
						r.Append (String.Format ("<h3>{0}: (<a href=\"{2}{1}\">Article Permalink</a>)</h3>", caption, article_file, blog_base));
					}
					continue;
				}
				line = Regex.Replace (line, "class=\"code\"", code_style);
				line = Regex.Replace (line, "class=\"code-csharp\"", code_csharp_style);
				line = Regex.Replace (line, "class=\"shell\"", shell_style);
				r.Append (line);
				r.Append ("\n");
			}
		}
		return r.ToString ();
	}

	public string PermaLink {
		get {
			return String.Format ("archive{2}{0:yyyy}/{0:MMM}-{0:dd}{1}.html", Date, extra, Category);
		}
	}
}

class Blog {
	public Config config;
	Hashtable category_entries = new Hashtable ();

	ArrayList entries = new ArrayList ();

	public int Entries {
		get {
			return entries.Count;
		}
	}
	
	public Blog (Config config)
	{
		this.config = config;

		LoadDirectory (new DirectoryInfo (config.BlogDirectory));

		Console.WriteLine ("Loaded: {0} days", entries.Count);

		entries.Sort ();
		foreach (DayEntry de in entries)
			AddCategory (category_entries, de);
	}

	void LoadDirectory (DirectoryInfo dir)
	{
		foreach (DirectoryInfo subdir in dir.GetDirectories ()) {
			LoadDirectory (subdir);
		}
		foreach (FileInfo file in dir.GetFiles ()) {
			if (!(file.Name.EndsWith (".html") || file.Name.EndsWith (".txt")))
				continue;
			DayEntry de = DayEntry.Load (this, file.FullName);
			if (de != null)
				entries.Add (de);
		}
	}

	// A category is always of the form "/((.+)/)*", e.g. /, /foo/, /foo/bar/
	// Add the DayEntry to its category and all parent categories
	void AddCategory (Hashtable hash, DayEntry day)
	{
		string category = day.Category;
		do {
			IList entries = (IList) hash [category];
			if (entries == null) {
				entries = new ArrayList ();
				hash [category] = entries;
			}
			entries.Add (day);
			int n = category.Length > 2 
				? category.LastIndexOf ('/', category.Length-2) 
				: -1;
			category = (n == -1) ? null : category.Substring (0, n+1);
		} while (category != null && category.Length > 0);
	}
	
	static DateTime LastDate = new DateTime (2004, 5, 19, 0, 0, 0);
	
	void Render (StreamWriter o, IList entries, int idx, string blog_base, bool include_daily_anchor, bool include_navigation)
	{
		DayEntry d = (DayEntry) entries [idx];

		string anchor = HttpUtility.UrlEncode (d.Date.ToString ()).Replace ('%','-').Replace ('+', '-');
		if (include_daily_anchor || d.Date < LastDate)
			o.WriteLine (String.Format ("<a name=\"{0}\"></a>", anchor));
		
		if (include_navigation){
			o.WriteLine ("<p>");
			o.Write (GetEntryNavigation (entries, idx, blog_base));
			o.WriteLine ("<p>");
		}
		
		o.WriteLine ("<h3><a href=\"{2}/{0}\" class=\"entryTitle\">{3}</a></h3>",
			     d.PermaLink, d.DateCaption, blog_base, d.Caption);
		o.WriteLine ("<div class='blogentry'>" + d.Body + "</div>");
		o.WriteLine ("<div class='footer'>Posted by {2} on <a href=\"{0}{1}\">{3}</a></div><p>",
			     blog_base, d.PermaLink, config.Copyright, d.DateCaption);
	}

	string GetEntryNavigation (IList entries, int idx, string blog_base)
	{
		DayEntry prev = (DayEntry) (idx > 0 ? entries [idx-1] : null);
		DayEntry next = (DayEntry) (idx+1 < entries.Count ? entries [idx+1] : null);

		StringBuilder nav = new StringBuilder ();

		if (prev != null)
			nav.Append (string.Format ("<a href=\"{0}{1}\">&laquo; {2}</a> | \n", 
						blog_base, prev.PermaLink, prev.Caption));
		nav.Append (string.Format ("<a href=\"{0}{1}\">Main</a>\n", 
					blog_base, config.BlogFileName));
		if (next != null)
			nav.Append (string.Format (" | <a href=\"{0}{1}\">{2} &raquo;</a> \n", 
						blog_base, next.PermaLink, next.Caption));
		
		return nav.ToString ();
	}

	void Render (StreamWriter o, IList entries, int start, int end, string blog_base, bool include_daily_anchor)
	{
		bool navigation = start + 1 == end;
		
		for (int i = start; i < end; i++){
			int idx = entries.Count - i - 1;
			if (idx < 0)
				return;
			
			Render (o, entries, idx, blog_base, include_daily_anchor, navigation);
		}
	}

	void RenderArticleList (StreamWriter o)
	{
		foreach (Article a in articles){
			o.WriteLine ("<a href=\"{0}\">{1}</a><br>", a.url, a.caption);
		}
	}
	
	void RenderHtml (string template, string output, string blog_base, IList entries,
			int start, int end)
	{
		Console.WriteLine ("Writing file {0}...", output);
		using (FileStream i = File.OpenRead (template), o = CreateFile (output)){
			StreamReader s = new StreamReader (i, Encoding.GetEncoding (config.InputEncoding));
			StreamWriter w = new StreamWriter (o, GetOutputEncoding ());
			string line;

			while ((line = s.ReadLine ()) != null){
				switch (line){
				case "@BLOG_ENTRIES@":
					Render (w, entries, start, end, blog_base, output == "all.html");
					break;
				case "@BLOG_ARTICLES@":
					RenderArticleList (w);
					break;

				default:
					line = line.Replace ("@BASEDIR@", blog_base);
					line = line.Replace ("@TITLE@", config.Title);
					line = line.Replace ("@DESCRIPTION@", config.Description);
					line = line.Replace ("@RSSFILENAME@", config.RSSFileName);
					line = line.Replace ("@EDITOR@", config.ManagingEditor);
					w.WriteLine (line);
					break;
				}

			}
			w.Flush ();
		}
	}

	// The default Encoding.GetEncoding ("utf-8") includes the BOM, 
	// which we don't want
	Encoding GetOutputEncoding ()
	{
		string encoding = config.OutputEncoding;
		Encoding e;
		if (encoding != null && encoding.ToLower().Replace ("-", "_").Equals ("utf_8"))
			e = new UTF8Encoding (false);
		else
			e = Encoding.GetEncoding (encoding);
		return e;
	}

	public void RenderHtml (string template, string output, int start, int end, string blog_base)
	{
		RenderHtml (template, output, blog_base, entries, start, end);
	}

	public void RenderArchive (string template)
	{
		for (int i = 0; i < Entries; i++){
			DayEntry d = (DayEntry) entries [i];

			string parent_dir = "../..";
			if (d.Category.Length > 0)
				parent_dir += Regex.Replace (d.Category, "[^/]+", "..");
			RenderHtml (template, d.PermaLink, entries.Count - i - 1, entries.Count - i, parent_dir);
		}

		foreach (DictionaryEntry de in category_entries) {
			string category = de.Key.ToString ();
			IList entries = (IList) de.Value;
			string parent_dir = ".." + Regex.Replace (category, "[^/]+", "..");
			RenderHtml (template, "archive" + category + "index.html",
					parent_dir, entries, 0, entries.Count);
		}
	}
	
	RssChannel MakeChannel ()
	{
		RssChannel c = new RssChannel ();

		c.Title = config.Title;
		c.Link = new Uri (config.BlogWebDirectory + "/" + config.BlogFileName);
		c.Description = config.Description;
		c.Copyright = config.Copyright;
		c.Generator = "lb#";
		c.ManagingEditor = config.ManagingEditor;
		c.PubDate = System.DateTime.Now;
		
		return c;
	}

	public void RenderRSS (RssVersion version, string output, int start, int end)
	{
		RssChannel channel = MakeChannel ();

		for (int i = start; i < end; i++){
			int idx = entries.Count - i - 1;
			if (idx < 0)
				continue;
			
			DayEntry d = (DayEntry) entries [idx];

			RssItem item = new RssItem ();
			item.Author = config.Author;
			item.Description = d.Body;
			item.Guid = new RssGuid ();
			item.Guid.Name = config.BlogWebDirectory + d.PermaLink;
			item.Link = new Uri (item.Guid.Name);
			item.Guid.PermaLink = DBBool.True;
			item.PubDate = d.Date;
			if (d.Caption == ""){
				Console.WriteLine ("No caption for: " + d.DateCaption);
				d.Caption = d.DateCaption;
			}
			item.Title = d.Caption;
			
			channel.Items.Add (item);
		}

		FileStream o = CreateFile (output);
		RssWriter w = new RssWriter (o, new UTF8Encoding (false));

		w.Version = version;

		w.Write (channel);
		w.Close ();
	}

	public void RenderRSS (string output, int start, int end)
	{
		RenderRSS (RssVersion.RSS20, output + ".rss2", start, end);
	}

	public class Article {
		public string url, caption;

		public Article (string u, string c)
		{
			url = u;
			caption = c;
		}
	}
	
	ArrayList articles = new ArrayList ();

	public void AddArticle (string url, string caption)
	{
		articles.Add (new Article (url, caption));
	}
	

	FileStream CreateFile (string file)
	{
		FileInfo info = new FileInfo (file);
		if (!info.Directory.Exists)
			info.Directory.Create ();
		return File.Create (file);
	}
}

class LB {

	static void Main ()
	{
		Config config = (Config) 
			new XmlSerializer (typeof (Config)).Deserialize (new XmlTextReader ("config.xml"));
		if (config.BlogImageBasedir == null || config.BlogImageBasedir == "")
			config.BlogImageBasedir = config.BlogWebDirectory;
		
		Blog b = new Blog (config);

		b.RenderHtml ("template", config.BlogFileName, 0, 30, "");
		b.RenderHtml ("template", "all.html", 0, b.Entries, "");
		b.RenderArchive ("template");
		
		b.RenderRSS (config.RSSFileName, 0, 30);
		File.Copy ("log-style.css", "texts/log-style.css", true);
	}
}
