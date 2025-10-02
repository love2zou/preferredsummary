import request from '@/utils/request'

// 图片数据相关的接口类型定义
export interface Picture {
  id: number
  appType: string
  imageCode: string
  imageName: string
  imagePath: string
  aspectRatio: number
  width: number | null
  height: number | null
  seqNo: number
  crtTime: string
  updTime: string
}

export interface PictureDto {
  appType: string
  imageCode: string
  imageName: string
  imagePath: string
  aspectRatio: number  // 保持为number，因为数据库存储的是decimal
  aspectRatioString?: string  // 添加这个字段用于后端解析
  width?: number
  height?: number
  seqNo: number
  fileExtension?: string  // 新增：文件扩展名字段
}

export interface PictureSearchParams {
  appType?: string
  imageName?: string
  imageCode?: string  // 添加这个字段
}

export interface PictureListParams {
  page?: number
  pageSize?: number
  appType?: string
  imageName?: string
  imageCode?: string  // 也在这里添加
}

export interface PagedResponse<T> {
  data: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

export interface UploadResponse {
  data: {
    url: string
    width: number
    height: number
    aspectRatio: string
  }
}

// 图片数据API服务
export const pictureApi = {
  // 获取图片列表
  getPictureList(params: PictureListParams): Promise<PagedResponse<Picture>> {
    return request.get('/api/picture/list', { params })
  },

  // 根据应用类型获取图片
  getPicturesByAppType(appType: string): Promise<Picture[]> {
    return request.get(`/api/picture/by-app-type/${appType}`)
  },

  // 根据ID获取图片详情
  getPictureById(id: number): Promise<Picture> {
    return request.get(`/api/picture/${id}`)
  },

  // 创建图片记录
  createPicture(data: PictureDto): Promise<Picture> {
    return request.post('/api/picture/create', data)
  },

  // 更新图片记录
  updatePicture(id: number, data: PictureDto): Promise<Picture> {
    return request.put(`/api/picture/${id}`, data)
  },

  // 删除图片
  deletePicture(id: number): Promise<void> {
    return request.delete(`/api/picture/${id}`)
  },

  // 上传图片
  uploadImage(file: File, aspectRatio?: string): Promise<UploadResponse> {
    const formData = new FormData()
    formData.append('file', file)
    if (aspectRatio) {
      formData.append('aspectRatio', aspectRatio)
    }
    return request.post('/api/picture/upload', formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    })
  },

  // 删除图片文件
  deleteImageFile(imagePath: string) {
    return request({
      url: `/api/picture/file?imagePath=${encodeURIComponent(imagePath)}`,
      method: 'delete'
    })
  }
}