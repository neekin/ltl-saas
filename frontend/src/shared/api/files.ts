import { http } from './http';

/** 文件上传元数据（对应后端 FileObjectDto，字段按 camelCase 序列化）。 */
export interface FileObjectDto {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  storageProvider: string;
  createdAt: string;
}

/**
 * 上传文件到对象存储（FILE-001）。
 * 使用 FormData，由 axios 自动设置 multipart/form-data 边界；JWT 由 http 拦截器附加。
 * 返回 fileId，供交付签收 POD、移动作业等引用。
 */
export async function uploadFile(file: File): Promise<FileObjectDto> {
  const form = new FormData();
  form.append('file', file);
  const response = await http.post<FileObjectDto>('/files', form);
  return response.data;
}

/** 获取文件内容 URL（供 <img src> 展示签收凭证等，JWT 需另行处理，此处仅返回地址）。 */
export function fileContentUrl(fileId: string): string {
  return `/api/files/${fileId}/content`;
}
