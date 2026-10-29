export interface Employee {
  departmentCode: string;
  employeeNumber: number;
  fullName: string;
  hireDate: string;
  monthlySalary: number;
  taxRate: number;
}

export interface Bonus {
  bonusId: number;
  departmentCode: string;
  departmentName: string;
  employeeNumber: number;
  fullName: string;
  period: string;
  bonusTypeId: number;
  bonusTypeDescription: string;
  value: number;
  reasonCode: string;
  reasonDescription: string;
  stateCode: string;
  stateDescription: string;
  grossAmount: number;
  taxAmount: number;
  netAmount: number;
}

export interface BonusFilters {
  departmentCode: string;
  employeeNumber: number | null;
  period: string;
  stateCode: string;
  pageIndex: number;
  pageSize: number;
}

export interface RegisterBonus {
  departmentCode: string;
  employeeNumber: number | null;
  period: string;
  bonusTypeId: number;
  value: number | null;
  reasonCode: string;
}
