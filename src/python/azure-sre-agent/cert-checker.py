import ssl
import socket
from urllib.parse import urlparse
from datetime import datetime, timezone

url = "https://bing.com"

def get_certificate_expiry(url: str) -> datetime:
    """Get the expiration date of the SSL certificate for a given URL."""
    parsed_url = urlparse(url)
    hostname = parsed_url.hostname
    port = parsed_url.port or 443

    context = ssl.create_default_context()
    with socket.create_connection((hostname, port), timeout=10) as sock:
        with context.wrap_socket(sock, server_hostname=hostname) as ssock:
            cert = ssock.getpeercert()
            expiry_date_str = cert["notAfter"]
            # Parse the date string (format: 'Mon DD HH:MM:SS YYYY GMT')
            expiry_date = datetime.strptime(expiry_date_str, "%b %d %H:%M:%S %Y %Z")
            return expiry_date


# Check certificate expiry
expiry_date = get_certificate_expiry(url)
days_until_expiry = (expiry_date - datetime.now(timezone.utc).replace(tzinfo=None)).days

print(f"URL: {url}")
print(f"Certificate expires: {expiry_date}")
print(f"Days until expiry: {days_until_expiry}")

if days_until_expiry < 30:
    print("⚠️ WARNING: Certificate expires in less than 30 days!")
elif days_until_expiry < 7:
    print("🚨 CRITICAL: Certificate expires in less than 7 days!")
else:
    print("✅ Certificate is valid")
