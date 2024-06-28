# UI/UX edits
- ## Common
1. add page icon
2. Add footer     -
3. responsiveness -
- ## adding a new page
5. add "page already exists" alert
6. Make the page name "required"  -
9. make "content is required" above content field  -
- ## Showing a page
10. make attachments appear
# Added Functionalties
1. Add a grammar and style check feature using api.languagetool.org
   
   ![grammar check](https://github.com/janaashraf/practical-aspnetcore/blob/net8.0/projects/sfa/wiki/Screenshot%202024-06-28%20214708.png)
3. Add a "show tag" feature which shows the topic of the page based on the content using www.uclassify.com

   ![show tag](https://github.com/janaashraf/practical-aspnetcore/blob/net8.0/projects/sfa/wiki/tags.png)

   - I tried to implement this feature using TensorFlow model and Flask API and it worked on postman but i faced an error when i tried to integrate it with the app and i couldn't solve it.
   ### My Code:
     ![code](https://github.com/janaashraf/practical-aspnetcore/blob/net8.0/projects/sfa/wiki/code.png)
   ### Postman:
     
     ![postman](https://github.com/janaashraf/practical-aspnetcore/blob/net8.0/projects/sfa/wiki/postman.png)
   ### The error:

     ![error](https://github.com/janaashraf/practical-aspnetcore/blob/net8.0/projects/sfa/wiki/error.png)


5. Add a button "Get help from wikipedia" that navigates to the desired page but on wikipedia to get some help if needed.

   ![wikipedia help](https://github.com/janaashraf/practical-aspnetcore/blob/net8.0/projects/sfa/wiki/wikipedia%20button.png)

# Code Improvements
1. Handling exceptions by using try/catch blocks and providing meaningful responses to users in each endpoint.
2. Extracting HandleError method that is used in all endpoints.
3. Extract methods written inside an endpoint to be seen and used in other endpoints such as "ToKebabCase()".
4. Fixed some typos such as Results.Problem("Progblem in saving page") and ("_logger.LogWarning($"Page id {id}  is a home page and elete operation on home page is not allowed")
