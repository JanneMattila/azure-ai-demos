import azure.functions as func
from markitdown import MarkItDown
import io

app = func.FunctionApp()

@app.route(route="Converter", auth_level=func.AuthLevel.ANONYMOUS)
def Converter(req: func.HttpRequest) -> func.HttpResponse:
    md = MarkItDown()
    result = md.convert_stream(io.BytesIO(req.get_body()))
    return func.HttpResponse(result.text_content, mimetype="text/markdown")
