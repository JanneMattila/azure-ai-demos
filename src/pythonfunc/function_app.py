import io
import azure.functions as func
import datetime
import json
import logging
import tempfile
import os
from markitdown import MarkItDown

app = func.FunctionApp()

@app.route(route="Converter", auth_level=func.AuthLevel.ANONYMOUS)
def Converter(req: func.HttpRequest) -> func.HttpResponse:
    md = MarkItDown()
    result = md.convert_stream(io.BytesIO(req.get_body()))
    return func.HttpResponse(result.text_content, mimetype="text/markdown")

    # # read raw uploaded bytes from the request
    # uploaded_bytes = req.get_body()
    # if not uploaded_bytes:
    #     return func.HttpResponse("No file uploaded", status_code=400)

    # # create a temporary file (Windows-safe) and write the upload to disk
    # suffix = ".xlsx"  # adjust suffix if needed or infer from request headers
    # tmp_path = None
    # try:
    #     with tempfile.NamedTemporaryFile(delete=False, suffix=suffix) as tmp:
    #         tmp.write(uploaded_bytes)
    #         tmp_path = tmp.name

    #     logging.info("Converting temporary file: %s", tmp_path)
    #     result = md.convert(tmp_path)
    # finally:
    #     if tmp_path and os.path.exists(tmp_path):
    #         try:
    #             os.remove(tmp_path)
    #         except Exception:
    #             logging.exception("Failed to remove temp file %s", tmp_path)

    # return func.HttpResponse(result.text_content, mimetype="text/markdown")