export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
}

export function getProblemMessage(error: unknown, fallback = 'Request failed'): string {
  if (!isRecord(error)) {
    return fallback;
  }

  const response = isRecord(error.response) ? error.response : undefined;
  const data = isRecord(response?.data) ? (response.data as ProblemDetails) : undefined;

  if (data?.errors) {
    const firstField = Object.keys(data.errors)[0];
    const firstMessage = firstField ? data.errors[firstField]?.[0] : undefined;
    if (firstMessage) {
      return firstMessage;
    }
  }

  if (typeof data?.detail === 'string' && data.detail.trim()) {
    return data.detail;
  }

  if (typeof data?.title === 'string' && data.title.trim()) {
    return data.title;
  }

  if (typeof error.message === 'string' && error.message.trim()) {
    return error.message;
  }

  return fallback;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}
