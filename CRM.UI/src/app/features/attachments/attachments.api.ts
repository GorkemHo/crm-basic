import { HttpClient, HttpEvent } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { AttachmentDto, AttachmentRecordType } from '../../core/api/models';

@Injectable({ providedIn: 'root' })
export class AttachmentsApi {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/attachments';

  list(recordType: AttachmentRecordType, recordId: number): Observable<AttachmentDto[]> {
    return this.http.get<AttachmentDto[]>(this.url, { params: { recordType, recordId } });
  }

  /** Emits upload progress events and finally the response with the saved attachment. */
  upload(recordType: AttachmentRecordType, recordId: number, file: File): Observable<HttpEvent<AttachmentDto>> {
    const form = new FormData();
    form.append('recordType', recordType);
    form.append('recordId', String(recordId));
    form.append('file', file, file.name);

    return this.http.post<AttachmentDto>(this.url, form, { reportProgress: true, observe: 'events' });
  }

  /** Downloads through the API (the bearer token is needed) and hands the file to the browser. */
  download(attachment: AttachmentDto): Observable<void> {
    return this.http.get(`${this.url}/${attachment.id}/download`, { responseType: 'blob' }).pipe(
      map((blob) => {
        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = attachment.fileName;
        link.click();
        setTimeout(() => URL.revokeObjectURL(link.href), 1000);
      }),
    );
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}

/** "1.2 MB" in the given language. */
export function formatSize(bytes: number, language: string): string {
  const units = ['byte', 'kilobyte', 'megabyte', 'gigabyte'] as const;
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit++;
  }
  return new Intl.NumberFormat(language, {
    style: 'unit',
    unit: units[unit],
    unitDisplay: 'short',
    maximumFractionDigits: unit === 0 ? 0 : 1,
  }).format(value);
}

/** Material Symbols icon for a file, by its content type. */
export function fileIcon(contentType: string): string {
  if (contentType.startsWith('image/')) return 'image';
  if (contentType === 'application/pdf') return 'picture_as_pdf';
  if (contentType.includes('spreadsheet') || contentType.includes('excel') || contentType === 'text/csv') return 'table_chart';
  if (contentType.includes('presentation') || contentType.includes('powerpoint')) return 'slideshow';
  if (contentType.includes('zip') || contentType.includes('compressed') || contentType.includes('rar')) return 'folder_zip';
  if (contentType.startsWith('message/') || contentType.includes('outlook')) return 'mail';
  return 'description';
}
