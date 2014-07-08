require(tm)
require(wordcloud)
require(RColorBrewer)
require(RJSONIO)
require(RCurl)
require(stringr)

working.folder <- "C:/Users/Lanibaldi/Documents/Twitter/Corpora"
setwd(working.folder)

sub.dirs <- list.dirs(path = working.folder, full.names = TRUE, 
                      recursive = FALSE)

set.seed(4363)

clean.text <- function(some_txt)
{
  some_txt = gsub("(RT|via)((?:\\b\\W*@\\w+)+)", "", some_txt)
  some_txt = gsub("@\\w+", "", some_txt)
  some_txt = gsub("[[:punct:]]", "", some_txt)
  some_txt = gsub("[[:digit:]]", "", some_txt)
  some_txt = gsub("http\\w+", "", some_txt)
  some_txt = gsub("[ \t]{2,}", "", some_txt)
  some_txt = gsub("^\\s+|\\s+$", "", some_txt)
  some_txt = gsub("amp", "", some_txt)
  # define "tolower error handling" function
  try.tolower = function(x)
  {
    y = NA
    try_error = tryCatch(tolower(x), error=function(e) e)
    if (!inherits(try_error, "error"))
      y = tolower(x)
    return(y)
  }
  
  some_txt = sapply(some_txt, try.tolower)
  some_txt = some_txt[some_txt != ""]
  names(some_txt) = NULL
  return(some_txt)
}

getSentiment <- function (text){
  
  text <- URLencode(text);
  
  #save all the spaces, then get rid of the weird characters that break the API, then convert back the URL-encoded spaces.
  text <- str_replace_all(text, "%20", " ");
  text <- str_replace_all(text, "%\\d\\d", "");
  text <- str_replace_all(text, " ", "%20");
  
  if (str_length(text) > 360){
    text <- substr(text, 0, 359);
  }
  ##########################################
  
  data <- getURL(paste("http://localhost/TwitterAnalyzer/sentiment?text=",text, sep=""))
  
  js <- fromJSON(data, asText=TRUE);
  
  # get mood probability
  sentiment = js$output$result
  
  ###################################
  return(list(sentiment=sentiment))
}

cur.dir <- format(Sys.Date(), "%d-%m-%Y")
file.remove(file.path(cur.dir, list.files(cur.dir, pattern="*.png")))

file.list <- file.path(cur.dir, list.files(cur.dir, pattern="*.txt"))

try.opentext <- function(x)
{
  txt <- NA
  try_error = tryCatch(file(x, open="rt"), error=function(e) e)
  if (!inherits(try_error, "error"))
    txt <- readLines(x)
  return(txt)
}

# get text
tweet_text <- sapply(file.list, try.opentext)
# clean text
tweet_clean <- clean.text(tweet_text)
tweet_num = length(tweet_clean)

tweet_df = data.frame(text=tweet_clean,
                      sentiment=rep("", tweet_num),
                      stringsAsFactors=FALSE)


# apply function getSentiment
sentiment = rep(0, tweet_num)
for (i in 1:tweet_num)
{
  tmp = getSentiment(tweet_clean[i])
  
  tweet_df$sentiment[i] = tmp$sentiment
  
  print(paste(i," of ", tweet_num))
  
}

# separate text by sentiment
sents = levels(factor(tweet_df$sentiment))
# get the labels and percents
getLabels <- function(x)
{
  len.text <- length((tweet_df[tweet_df$sentiment==x,])$text)
  len.sent <- length(tweet_df$sentiment)
  paste(x,format(round((len.text/len.sent*100),2),nsmall=2),"%")  
}
labels <- lapply(sents, getLabels)
                 
nemo = length(sents)
emo.docs = rep("", nemo)
for (i in 1:nemo)
{
  tmp = tweet_df[tweet_df$sentiment == sents[i],]$text
  
  emo.docs[i] = paste(tmp,collapse=" ")
}


# remove stopwords
emo.docs = removeWords(emo.docs, stopwords("italian"))
emo.docs = removeWords(emo.docs, stopwords("english"))
corpus = Corpus(VectorSource(emo.docs))
tdm = TermDocumentMatrix(corpus)
tdm = as.matrix(tdm)
colnames(tdm) = labels

file.name <- paste(paste("comp_wordcloud", format(Sys.time(), "%Y%m%d%H%M"), sep="_"),"png", sep=".")
png(file.path(cur.dir, file.name), width=1280,height=800)

# comparison word cloud
comparison.cloud(tdm, colors = brewer.pal(nemo, "Dark2"),
                 scale = c(5,.7), random.order = FALSE, 
                 title.size = 1.5)

dev.off() 



