// Shapes of the content endpoints (src/api/DocIntel.Api/Content/Dtos.cs).

export type Pdf = { status: 'None' | 'Pending' | 'Done' | 'Failed'; error: string | null }

export type Version = {
  id: string
  number: string
  status: 'Draft' | 'Published' | 'Archived'
  fileName: string
  contentType: string
  sizeBytes: number
  authorName: string
  createdAt: string
  comment: string | null
  sourceNumber: string | null
  pdf: Pdf
}

export type Checkout = { userName: string; takenAt: string; baseVersionNumber: string; isMine: boolean }

export type Category = { id: number; name: string }

export type DocumentDetail = {
  id: string
  title: string
  description: string | null
  category: Category
  ownerName: string
  createdAt: string
  isRetired: boolean
  retiredAt: string | null
  retiredByName: string | null
  publishedVersion: Version | null
  latestVersion: Version | null
  checkouts: Checkout[]
  myCheckout: Checkout | null
  canEdit: boolean
  canAdminister: boolean
  canPublish: boolean
  groups: string[]
}

export type DocumentSummary = {
  id: string
  title: string
  category: string
  ownerName: string
  isRetired: boolean
  publishedNumber: string | null
  latestNumber: string | null
  latestStatus: Version['status'] | null
  updatedAt: string
  checkoutCount: number
}

// Accepted formats (FileRules.cs); the API checks again.
export const acceptedExtensions = ['.pdf', '.txt', '.md', '.csv', '.html', '.xml', '.json', '.odt', '.ods', '.odp', '.odg']
export const maxFileBytes = 25 * 1024 * 1024
