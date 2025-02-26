# Added Functionalties
1. Added a grammar and style check feature using api.languagetool.org
   
   ![grammar check](https://github.com/janaashraf/practical-aspnetcore/blob/net8.0/projects/sfa/wiki/Screenshot%202024-06-28%20214708.png)
2. Added a "show tag" feature which shows the topic of the page based on the content using www.uclassify.com

   ![show tag](https://github.com/janaashraf/practical-aspnetcore/blob/net8.0/projects/sfa/wiki/tags.png)

   - I tried to implement this feature using TensorFlow model and Flask API and it worked on postman but i faced an error when i tried to integrate it with the app and i couldn't solve it.
   ### My Code:
     ![code](https://github.com/janaashraf/practical-aspnetcore/blob/net8.0/projects/sfa/wiki/code.png)
   ### Postman:
     
     ![postman](https://github.com/janaashraf/practical-aspnetcore/blob/net8.0/projects/sfa/wiki/postman.png)
   ### The error:

     ![error](https://github.com/janaashraf/practical-aspnetcore/blob/net8.0/projects/sfa/wiki/error.png)


3. Added a button "Get help from wikipedia" that navigates to the desired page but on wikipedia to get some help if needed.

   ![wikipedia help](https://github.com/janaashraf/practical-aspnetcore/blob/net8.0/projects/sfa/wiki/wikipedia%20button.png)


# UI/UX edits
- ## Common
1. Added footer  
2. Added responsiveness to handle mobile view
3. Tried to add page icon it is found but cannot be shown 
- ## Adding a new page
4. Added "page already exists" alert if the page name is repeated instead of redirecting
5. Made the page name "required"  
6. Made "content is required" above content field  
- ## Showing a page
7. Made attachments appear instead of just showing the link, but when i tried to render the images directly i got an error "Error while handling request for '/attachment System.IO.IOException: The process cannot access the file 'E:\wiki\wiki.db' because it is being used by another process." that i couldn't understand, so i used a modal.
 
# Code Improvements
1. Handled exceptions by using try/catch blocks and providing meaningful responses to users in each endpoint.
2. Extracted HandleError method that is used in all endpoints.
3. Extracted methods written inside an endpoint to be seen and used in other endpoints such as "ToKebabCase()".
4. Fixed some typos such as Results.Problem("Progblem in saving page") and ("_logger.LogWarning($"Page id {id}  is a home page and elete operation on home page is not allowed")
5. Added comments to improve code readability
