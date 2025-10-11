// 文件：userService.ts 中的 userService 对象
import adminApi from '@/services/adminApi'

export interface AdminUser {
    id: number
    userName: string
    email: string
    phoneNumber?: string | null
    profilePictureUrl?: string | null
    bio?: string | null
    userTypeCode?: string | null
    userTypeName?: string | null
    userTypeHexColor?: string | null
    userTypeRgbColor?: string | null
    userToSystemCode?: string | null
    isActive: boolean
    isEmailVerified?: boolean
    crtTime: string
    updTime?: string
    lastLoginTime?: string | null
}

export const userService = {
    // 获取用户详情
    getDetail(id: number) {
        return adminApi.get(`/user/detail/${id}`)
    },
    // 更新用户信息（部分字段）
    update(id: number, data: Partial<AdminUser>) {
        return adminApi.put(`/user/update/${id}`, data)
    },
    // 获取用户列表（分页），用于筛选教练候选
    list(page = 1, size = 100, params?: { username?: string; email?: string; isActive?: boolean }) {
        return adminApi.get('/user/list', { params: { page, size, ...(params || {}) } })
    },
    uploadAvatar(id: number, file: File) {
        const form = new FormData()
        form.append('file', file)            // 后端 PictureController 要求字段名为 file
        form.append('aspectRatio', '1:1')    // 可选：示例宽高比
        return adminApi.post('/picture/upload', form, {
            headers: { 'Content-Type': 'multipart/form-data' }
        })
    }
}