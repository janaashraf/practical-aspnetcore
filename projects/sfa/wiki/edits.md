# UI/UX edits
- ## Common
1. add page icon
3. responsiveness
4. increase nav bar margin bottom
- ## adding a new page
5. add "page already exists" alert
6. Make the page name "required"
7. Add footer
9. make "content is required" bigger and next to content field
- ## Showing a page
10. make attachments appear
11. Replace the edit button with pen icon, position it next to the page's title on the left, and add a hover text that says 'edit page'
12. Replace the delete button with rubbish icon, position it next to the page's title on the right, and add a hover text that says 'delete page'
- ## Editing a page
13. Place exisiting attachments above submit button
- ## Home page
14. Replace the edit button with pen icon, position it next to the page's title on the left, and add a hover text that says 'edit page'

# Code Improvements
1. Handling exceptions by using try/catch blocks and providing meaningful responses to users in each endpoint.
2. Extracting HandleError method that is used in all endpoints.
3. Extract methods written inside an endpoint to be seen and used in other endpoints such as "ToKebabCase()".
4. Fixed some typos such as Results.Problem("Progblem in saving page")
