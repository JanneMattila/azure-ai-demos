import azure.functions as func
from markitdown import MarkItDown
import io
import requests

app = func.FunctionApp()

@app.function_name(name="Converter")
@app.route(route="", auth_level=func.AuthLevel.ANONYMOUS)
def Converter(req: func.HttpRequest) -> func.HttpResponse:
    md = MarkItDown()
    result = md.convert_stream(io.BytesIO(req.get_body()))
    return func.HttpResponse(result.text_content, mimetype="text/markdown")

@app.function_name(name="Requestor")
@app.route(route="", auth_level=func.AuthLevel.ANONYMOUS)
def Requestor(req: func.HttpRequest) -> func.HttpResponse:
    # Extract the request url from the query parameters
    request_url = req.params.get("request_url")
    if not request_url:
        return func.HttpResponse(
            "Please pass a request_url on the query string",
            status_code=400
        )
    # make an HTTP GET request to the provided URL
    response = requests.get(request_url)
    return func.HttpResponse(response.content, status_code=response.status_code, mimetype=response.headers.get('Content-Type', 'application/octet-stream'))