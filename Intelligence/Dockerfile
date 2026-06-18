# syntax=docker/dockerfile:1

FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1
ENV INTELLIGENCE_ENV=production
ENV INTELLIGENCE_API_PREFIX=/api/v1
ENV INTELLIGENCE_ENABLE_DEBUG=false

WORKDIR /app

COPY Intelligence/requirements.txt ./requirements.txt
RUN pip install --no-cache-dir -r requirements.txt

COPY Intelligence/app ./app
RUN addgroup --system app \
    && adduser --system --ingroup app app \
    && chown -R app:app /app

USER app

EXPOSE 8020

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8020"]
