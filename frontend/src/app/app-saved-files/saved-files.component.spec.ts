import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AppSavedFilesComponent } from './saved-files.component';

describe('AppSavedFilesComponent', () => {
  let component: AppSavedFilesComponent;
  let fixture: ComponentFixture<AppSavedFilesComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppSavedFilesComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(AppSavedFilesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
