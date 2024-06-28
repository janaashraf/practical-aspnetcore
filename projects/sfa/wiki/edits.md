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
1. Add a button "Get help from wikipedia" that navigates to the desired page but on wikipedia to get some help if needed.  -
# Code Improvements
1. Handling exceptions by using try/catch blocks and providing meaningful responses to users in each endpoint.
2. Extracting HandleError method that is used in all endpoints.
3. Extract methods written inside an endpoint to be seen and used in other endpoints such as "ToKebabCase()".
4. Fixed some typos such as Results.Problem("Progblem in saving page") and ("_logger.LogWarning($"Page id {id}  is a home page and elete operation on home page is not allowed")
