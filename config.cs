using System.Xml.Serialization;

public class Config {
	[XmlAttribute] public string Author;
	[XmlAttribute] public string BlogDirectory;
	[XmlAttribute] public string BlogFileName;
	[XmlAttribute] public string BlogWebDirectory;
	[XmlAttribute] public string Copyright;
	[XmlAttribute] public string Description;
	[XmlAttribute] public string ManagingEditor;
	[XmlAttribute] public string Link;
	[XmlAttribute] public string Title;
	[XmlAttribute] public string RSSFileName;
}