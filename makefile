#REMOTE_DIRECTORY=miguel@www.tirania.org:tirania/blog
BASE_REMOTE=miguel@45.55.96.137:web/tirania.org/
REMOTE_BLOG=$(BASE_REMOTE)blog
REMOTE_MONOMAC=$(BASE_REMOTE)monomac
STYLE_HASH=$(shell shasum -a 256 startbootstrap/css/miguel-style.css | cut -c1-12)
STYLE_FILE=blog-$(STYLE_HASH).css

ASSEMBLIES=	-r:RSS.NET.dll	\
		-r:System.Web	

lb.exe: lb.cs config.cs Markdig.dll
	mcs $(ASSEMBLIES) -debug Markdown.cs lb.cs config.cs -out:lb.exe -r:Markdig.dll

Markdig.dll: Markdig.nuget
	unzip -j Markdig.nuget lib/net40/Markdig.dll
	touch Markdig.dll

Markdig.nuget:
	curl -L -o Markdig.nuget https://www.nuget.org/api/v2/package/Markdig/0.15.0

b: lb.exe
	mono --debug lb.exe -c config.xml -p output

#
# Builds the standard blog
#
c: lb.exe twentyten/blog-template.standard
	-mkdir new; cp twentyten/* new
	mono --debug lb.exe -c config.xml -p new -b twentyten/blog-template.standard -e twentyten/entry-template

#
# Builds the standard blog, with bootstrap
#
k: lb.exe startbootstrap/blog-template startbootstrap/summary-template
	-mkdir -p sb-gen/css
	cp startbootstrap/css/miguel-style.css sb-gen/css/$(STYLE_FILE)
	sed 's/@STYLE_FILE@/$(STYLE_FILE)/g' startbootstrap/blog-template > sb-gen/blog-template
	mono --debug lb.exe -c config.xml -p sb-gen -b sb-gen/blog-template -e startbootstrap/entry-template.start -s startbootstrap/summary-template

#
# Builds the MonoMac blog
#

m: lb.exe twentyten/blog-template.monomac
	-mkdir monomac; cp twentyten/* monomac
	mono --debug lb.exe -c monomac.xml -p monomac -b twentyten/blog-template.monomac -e twentyten/entry-template

twentyten/blog-template.standard: twentyten/blog-template twentyten/widgets.standard
	sed -e '/@WIDGETS@/r twentyten/widgets.standard' -e 'x;$G' -e 's/@WIDGETS@//' twentyten/blog-template > $@

twentyten/blog-template.monomac: twentyten/blog-template twentyten/widgets.monomac makefile
	sed -e '/@WIDGETS@/r twentyten/widgets.monomac' -e 'x;$G' -e 's/@WIDGETS@//' -e 's,@BLOGWEBDIR@/pic2.jpg,https://monomac.wordpress.com/wp-content/themes/pub/twentyten/images/headers/sunset.jpg,' twentyten/blog-template > $@

clean:
	rm -fr *.exe output

push: pushc

# pushb is deprecated
pushb: b
	make do-push DIR=output REMOTE_DIRECTORY=$(REMOTE_BLOG)

pushc: k 
	rsync -zu --checksum -pr -v --rsh=ssh sb-gen/css/$(STYLE_FILE) $(REMOTE_BLOG)/css/
	make do-push DIR=sb-gen REMOTE_DIRECTORY=$(REMOTE_BLOG)

pushm: m
	make do-push DIR=monomac REMOTE_DIRECTORY=$(REMOTE_MONOMAC)

r:
	rsync twentyten/* $(REMOTE_DIRECTORY)

do-push:
	chmod 644 $(DIR)/archive/*/*.html
	chmod 644 $(DIR)/*html $(DIR)/*rss2 
	rsync -zu --checksum -pr -v --rsh=ssh texts prettyprint.js log-style.css 		\
	$(DIR)/archive $(DIR)/*.rss2 $(DIR)/index.html $(DIR)/page*.html $(DIR)/all.html	\
		$(wildcard $(DIR)/sitemap.xml)  \
		$(wildcard $(DIR)/*.css) $(wildcard $(DIR)/*.gif) \
	$(REMOTE_DIRECTORY)

check-update:
	-mv config.xml ._config.xml
	cp config.xml.check config.xml
	mono --debug lb.exe --blog-directory=`pwd`/test/in --prefix=test/out \
		--blog-template=template.test --entry-template=entry.test \
		--rss-filename=test
	-rm config.xml
	-mv ._config.xml config.xml

check:
	-rm -Rf test/tmp
	-mv config.xml ._config.xml
	cp config.xml.check config.xml
	mono --debug lb.exe --blog-directory=`pwd`/test/in --prefix=test/tmp \
		--blog-template=template.test --entry-template=entry.test \
		--rss-filename=test
	diff -ru --exclude=\.svn test/out test/tmp 
	-rm config.xml
	-mv ._config.xml config.xml

