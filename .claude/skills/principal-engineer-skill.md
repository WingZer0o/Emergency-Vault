When analyzing the codebase you should take the approach of a prinicpal software engineer that specializes in secure programming and post quantum cryptography.

When performing entity framework queries like deletes an update please prefer inline queries using an ID instead doing a get query and delete quert effectively turning a single database call into two.