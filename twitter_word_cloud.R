require(tm)
require(wordcloud)
require(RColorBrewer)
require(RJSONIO)

working.folder <- "C:/Users/Lanibaldi/Documents/Twitter/Corpora"
setwd(working.folder)

sub.dirs <- list.dirs(path = working.folder, full.names = TRUE, 
                      recursive = FALSE)

set.seed(4363)

#idx = length(sub.dirs)
#while (idx > 0){
#  cur.dir <- sub.dirs[[idx]]
#  idx <- idx - 1

  cur.dir <- format(Sys.Date(), "%d-%m-%Y")

  message(cur.dir)
  docs <- DirSource(cur.dir, encoding="ISO8859-1", ignore.case = TRUE)
  ap.corpus <- Corpus(docs, readerControl = list(reader = docs$DefaultReader, 
                                                 language = "it"))

  ap.corpus <- tm_map(ap.corpus, tolower)
  #ap.corpus <- tm_map(ap.corpus, function(x) removeWords(x, c("http://")))
  #ap.corpus <- tm_map(ap.corpus, function(x) removeWords(x, c("png", "jpg")))
  ap.corpus <- tm_map(ap.corpus, removePunctuation)
  ap.corpus <- tm_map(ap.corpus, function(x) removeWords(x, stopwords("italian")))
  ap.corpus <- tm_map(ap.corpus, function(x) removeWords(x, stopwords("english")))

  ap.tdm <- TermDocumentMatrix(ap.corpus)    
  ap.tdm <- removeSparseTerms(ap.tdm, 0.9999)
  ap.dict <- Dictionary(ap.tdm)
  #save(ap.dict, file="dict.RData")
  ap.m <- as.matrix(ap.tdm)
  ap.v <- sort(rowSums(ap.m),decreasing=TRUE)
  ap.d <- data.frame(word = names(ap.v),freq=ap.v)
  ap.f <- floor(length(names(ap.v))/100)
  table(ap.d$freq)  
  
  file.name <- paste(paste("wordcloud", format(Sys.time(), "%Y%m%d%H%M"), sep="_"),"png", sep=".")
  png(file.path(cur.dir, file.name), width=1280,height=800)
  wordcloud(ap.d$word,ap.d$freq, scale=c(7,.5),min.freq=ap.f,
            max.words=1000, random.order=FALSE, rot.per=.15, 
            colors=brewer.pal(8,"Dark2") )
  dev.off()  
#}



