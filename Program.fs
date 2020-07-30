// Learn more about F# at http://fsharp.org
open System
open System.Collections.Generic
open System.Linq
open System.Net
open System.Net.Http
open System.Threading.Tasks.Dataflow
open System.Threading.Tasks

// here's an example of the full syntax for waiting on .NET Tasks
let f: string -> Task<string> = fun (uri: string) ->
    async {
      Console.WriteLine "Downloading stuff"
      let! s = (new HttpClient()).GetStringAsync uri |> Async.AwaitTask
      let! s2 = (new HttpClient()).GetStringAsync uri |> Async.AwaitTask
      return s + s2
    } |> Async.StartAsTask


// Downloads the requested resource as a string.
let downloadString = TransformBlock<string, string>(fun uri ->
    printfn "Downloading '%s'..." uri
    let handler = new HttpClientHandler( AutomaticDecompression = (DecompressionMethods.GZip ||| DecompressionMethods.Deflate) )
    (new HttpClient(handler)).GetStringAsync uri
)

// Separates the specified text into an array of words.
let createWordList = TransformBlock<string, string[]>(fun text ->
    printfn "Creating word lists..."

    // Remove common punctuation by replacing all non-letter characters
    // with a space character.
    let tokens: char array = text |> Seq.map (fun c -> if Char.IsLetter c then c else ' ') |> Seq.toArray
    let text : string = new string(tokens)
    // note that the below produces a string with weird contents (without "new")
    // let text : string = string(tokens)

    // Separate the text into an array of words.
    text.Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries)
)

// Removes short words and duplicates.
let filterWordList = TransformBlock<string[], string[]>(fun words ->
   printfn "Filtering word list..."

   words
   |> Seq.where(fun word -> word.Length > 3)
   |> Seq.distinct
   |> Seq.toArray
);

// Finds all words in the specified collection whose reverse also
// exists in the collection.
let findReversedWords = TransformManyBlock<string[], string>(fun words ->
   printfn "Finding reversed words..."
   printfn "Processing %i words" words.Length

   let wordsSet = HashSet<string>(words)

   query {
       for word in words.AsParallel() do
       let reverse = new string(word.Reverse().ToArray())
       where (word <> reverse && wordsSet.Contains reverse)
       select word
   }
)

// Prints the provided reversed words to the console.
let printReversedWords = ActionBlock<string>(fun reversedWord ->
   let normalWord = new string(reversedWord.Reverse() |> Seq.toArray)
   printfn "Found reversed words %s/%s" reversedWord normalWord
)

let linkOptions = DataflowLinkOptions ( PropagateCompletion = true )

[<EntryPoint>]
let main argv =
    downloadString.LinkTo(createWordList, linkOptions) |> ignore
    createWordList.LinkTo(filterWordList, linkOptions) |> ignore
    filterWordList.LinkTo(findReversedWords, linkOptions) |> ignore
    findReversedWords.LinkTo(printReversedWords, linkOptions) |> ignore

    // Process "The Iliad of Homer" by Homer.
    downloadString.Post("http://www.gutenberg.org/cache/epub/16452/pg16452.txt") |> ignore

    // Mark the head of the pipeline as complete.
    downloadString.Complete() |> ignore

    // Wait for the last block in the pipeline to process all messages.
    printReversedWords.Completion.Wait() |> ignore

    0 // return an integer exit code
